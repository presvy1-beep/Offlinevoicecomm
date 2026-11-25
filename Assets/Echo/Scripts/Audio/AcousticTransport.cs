using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Echo {
public enum TransmissionState { Idle, Encoding, Playing, PlaybackCompleted, Failed }
public sealed class AcousticTransport : MonoBehaviour {
    public event Action<string> Received;
    public event Action<string> StatusChanged;
    public event Action<string> TransmissionCompleted;
    public event Action<string> TransmissionFailed;
    public readonly SpectrumAnalyser Monitor = new SpectrumAnalyser();
    public int CaptureRate { get; private set; }
    public bool Listening { get; private set; }
    public TransmissionState State { get; private set; }
    public bool Transmitting => State == TransmissionState.Encoding || State == TransmissionState.Playing;
    public float Progress { get; private set; }
    GGWaveCodec codec;
    AudioClip microphone, outgoing;
    AudioSource speaker;
    string device;
    int cursor, pending;
    readonly float[] frame = new float[GGWaveCodec.Frame];
    readonly float[] monitorFrame = new float[GGWaveCodec.Frame];
    float nextVisual;
    float receivedUntil;
    float terminalStatusUntil;
    bool resumeListening;
    bool diagnosticTone;
    string pendingMessage;
    float playbackStartedAt, expectedDuration;
    bool foreground = true, pendingPermissionStart;
#if UNITY_ANDROID && !UNITY_EDITOR
    bool permissionPending;
    PermissionCallbacks permissionCallbacks;
#endif
    void Awake() {
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false; speaker.loop = false; speaker.spatialBlend = 0;
        speaker.volume = 1; speaker.mute = false; speaker.outputAudioMixerGroup = null;
        Debug.Log("ECHO audio: listener count=" + FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length +
                  ", AudioListener.volume=" + AudioListener.volume + ", output sample rate=" + AudioSettings.outputSampleRate);
    }
    public void StartListening() {
        if (Listening) return;
        if (!foreground) { pendingPermissionStart = true; return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
            if (permissionPending) return;
            permissionPending = true;
            StatusChanged?.Invoke("Microphone permission needed");
            permissionCallbacks = new PermissionCallbacks();
            permissionCallbacks.PermissionGranted += _ => {
                permissionPending = false; permissionCallbacks = null;
                if (foreground) StartListening(); else pendingPermissionStart = true;
            };
            permissionCallbacks.PermissionDenied += _ => { permissionPending = false; permissionCallbacks = null; StatusChanged?.Invoke("Microphone permission denied"); };
            permissionCallbacks.PermissionDeniedAndDontAskAgain += _ => { permissionPending = false; permissionCallbacks = null; StatusChanged?.Invoke("Microphone permission denied"); };
            Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
            return;
        }
#else
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) { StartCoroutine(RequestMicrophone()); return; }
#endif
        if (Microphone.devices.Length == 0) { StatusChanged?.Invoke("No microphone available"); return; }
        try {
            device = Microphone.devices[0];
            int min, max; Microphone.GetDeviceCaps(device, out min, out max);
            int requested = max > 0 && max < 48000 ? max : 48000;
            microphone = Microphone.Start(device, true, 2, requested);
            if (microphone == null) throw new Exception("Microphone.Start failed");
            CaptureRate = microphone.frequency;
            codec = new GGWaveCodec(CaptureRate);
            cursor = 0; pending = 0; Listening = true; StatusChanged?.Invoke("Listening");
        } catch (DllNotFoundException e) { StopListening(); StatusChanged?.Invoke("Native library unavailable: " + e.Message); }
          catch (Exception e) { StopListening(); StatusChanged?.Invoke("Audio initialization error: " + e.Message); }
    }
    IEnumerator RequestMicrophone() {
        StatusChanged?.Invoke("Microphone permission needed");
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (Application.HasUserAuthorization(UserAuthorization.Microphone)) StartListening();
        else StatusChanged?.Invoke("Microphone permission denied");
    }
    public void StopListening() {
        if (device != null && Microphone.IsRecording(device)) Microphone.End(device);
        device = null; microphone = null; Listening = false; pending = 0;
        codec?.Dispose(); codec = null;
        if (!Transmitting) StatusChanged?.Invoke("Idle");
    }
    public void Transmit(string text, bool high, int speed, int volume) {
        if (Transmitting) throw new InvalidOperationException("Playback already active");
        State = TransmissionState.Encoding;
        StatusChanged?.Invoke("Encoding");
        bool wasListening = Listening;
        resumeListening = wasListening;
        if (wasListening) StopListening();
        try {
            using (var encoder = new GGWaveCodec(48000)) {
                var samples = encoder.Encode(text, high, speed, volume);
                PlaySamples(samples, text, false, GGWaveCodec.ProtocolId(high, speed), volume);
            }
        } catch (Exception e) { FailPlayback(e.Message); throw; }
    }
    public void PlayTestTone() {
        if (Transmitting) throw new InvalidOperationException("Playback already active");
        resumeListening = Listening;
        if (Listening) StopListening();
        try {
            const int rate = 48000;
            var samples = new float[rate / 2];
            for (int i = 0; i < samples.Length; i++) {
                float envelope = Mathf.Min(1, i / 1200f, (samples.Length - 1 - i) / 1200f);
                samples[i] = .08f * Mathf.Max(0, envelope) * Mathf.Sin(2 * Mathf.PI * 440 * i / rate);
            }
            PlaySamples(samples, null, true, -1, 8);
        } catch (Exception e) { FailPlayback(e.Message); throw; }
    }
    void PlaySamples(float[] samples, string message, bool tone, int protocol, int volume) {
        if (samples == null || samples.Length < 1) throw new InvalidOperationException("Encoder returned no samples");
        double squares = 0; float peak = 0;
        foreach (float sample in samples) { if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidOperationException("Invalid PCM sample"); float a = Mathf.Abs(sample); peak = Mathf.Max(peak, a); squares += sample * sample; }
        float rms = (float)Math.Sqrt(squares / samples.Length);
        if (peak < .001f || rms < .0001f) throw new InvalidOperationException("Encoded waveform is silent");
        Debug.Log("ECHO TX: payloadBytes=" + (message == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(message)) +
                  " protocol=" + (tone ? "test-tone" : protocol.ToString()) + " volume=" + volume +
                  " encodedBytes=" + samples.Length * sizeof(float) + " pcmSamples=" + samples.Length +
                  " sampleRate=48000 duration=" + (samples.Length / 48000f).ToString("F3") +
                  " peak=" + peak.ToString("F4") + " rms=" + rms.ToString("F4"));
        outgoing = AudioClip.Create(tone ? "ECHO test tone" : "ECHO transmission", samples.Length, 1, 48000, false);
        if (outgoing == null || outgoing.samples != samples.Length || outgoing.channels != 1 || outgoing.frequency != 48000)
            throw new InvalidOperationException("AudioClip creation failed");
        if (!outgoing.SetData(samples, 0)) throw new InvalidOperationException("AudioClip.SetData failed");
        Monitor.Analyze(samples, Math.Min(samples.Length, SpectrumAnalyser.Size));
        speaker.clip = outgoing; speaker.volume = 1; speaker.mute = false; speaker.spatialBlend = 0;
        if (!speaker.enabled || !gameObject.activeInHierarchy) throw new InvalidOperationException("AudioSource is inactive");
        if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1) throw new InvalidOperationException("Exactly one AudioListener is required");
        if (AudioListener.pause || AudioListener.volume <= 0) throw new InvalidOperationException("AudioListener is muted or paused");
        diagnosticTone = tone; pendingMessage = message; expectedDuration = outgoing.length;
        speaker.Play(); playbackStartedAt = Time.unscaledTime; Progress = 0;
        State = TransmissionState.Playing; StatusChanged?.Invoke(tone ? "Playing test tone" : "Playing");
        Debug.Log("ECHO TX: AudioClip created=" + outgoing.name + " AudioSource.Play called, isPlaying=" + speaker.isPlaying +
                  " sourceVolume=" + speaker.volume + " listenerVolume=" + AudioListener.volume + " mixer=" + (speaker.outputAudioMixerGroup == null ? "default" : speaker.outputAudioMixerGroup.name));
    }
    void FinishPlayback() {
        Debug.Log("ECHO TX: playback completed after " + (Time.unscaledTime - playbackStartedAt).ToString("F3") + "s");
        string completed = pendingMessage;
        bool tone = diagnosticTone;
        CleanupPlayback(); State = TransmissionState.PlaybackCompleted;
        if (!tone) TransmissionCompleted?.Invoke(completed);
        ResumeReception();
        StatusChanged?.Invoke(tone ? "Test tone completed" : "Playback completed");
        terminalStatusUntil = Time.unscaledTime + 2;
    }
    void FailPlayback(string reason) {
        Debug.LogError("ECHO TX failed: " + reason);
        string failed = pendingMessage;
        CleanupPlayback(); State = TransmissionState.Failed;
        TransmissionFailed?.Invoke(failed);
        ResumeReception();
        StatusChanged?.Invoke("Playback failed: " + reason);
        terminalStatusUntil = Time.unscaledTime + 4;
    }
    void CleanupPlayback() {
        speaker.Stop(); speaker.clip = null;
        if (outgoing != null) Destroy(outgoing);
        outgoing = null; pendingMessage = null; diagnosticTone = false; Progress = 0;
    }
    void ResumeReception() {
        if (resumeListening && foreground) StartListening();
        resumeListening = false;
    }
    void Update() {
        if (terminalStatusUntil > 0 && Time.unscaledTime >= terminalStatusUntil) {
            terminalStatusUntil = 0; StatusChanged?.Invoke(Listening ? "Listening" : "Idle");
        }
        if (Transmitting) {
            Progress = outgoing != null && outgoing.samples > 0 ? (float)speaker.timeSamples / outgoing.samples : 0;
            if (outgoing != null && Time.unscaledTime >= nextVisual) { outgoing.GetData(monitorFrame, Math.Min(speaker.timeSamples, Math.Max(0, outgoing.samples - monitorFrame.Length))); Monitor.Analyze(monitorFrame, monitorFrame.Length); nextVisual = Time.unscaledTime + 0.05f; }
            if (!speaker.isPlaying) {
                float elapsed = Time.unscaledTime - playbackStartedAt;
                if (elapsed < .2f) return; // AudioSource can take a frame to enter playback.
                if (elapsed >= expectedDuration - .12f) FinishPlayback();
                else FailPlayback("AudioSource stopped at " + elapsed.ToString("F2") + "s of " + expectedDuration.ToString("F2") + "s");
            }
            return;
        }
        if (!Listening || microphone == null || !Microphone.IsRecording(device)) return;
        if (receivedUntil > 0 && Time.unscaledTime >= receivedUntil) { receivedUntil = 0; StatusChanged?.Invoke("Listening"); }
        int position = Microphone.GetPosition(device);
        if (position < 0) return;
        int available = position >= cursor ? position - cursor : microphone.samples - cursor + position;
        // A full capture buffer was missed; reset rather than process stale data.
        if (available > microphone.samples - GGWaveCodec.Frame) { cursor = position; pending = 0; return; }
        while (available >= GGWaveCodec.Frame) {
            ReadMono(frame, cursor, GGWaveCodec.Frame);
            cursor = (cursor + GGWaveCodec.Frame) % microphone.samples;
            available -= GGWaveCodec.Frame;
            try {
                int result = codec.Decode(frame, frame.Length, out string text);
                if (result > 0) { Received?.Invoke(text); StatusChanged?.Invoke("Receiving"); receivedUntil = Time.unscaledTime + 1.2f; }
                // -1 is a recoverable acoustic decoding error; the decoder continues listening.
            } catch (Exception e) { StatusChanged?.Invoke("Decode error: " + e.Message); }
            if (Time.unscaledTime >= nextVisual) { Monitor.Analyze(frame, frame.Length); nextVisual = Time.unscaledTime + 0.05f; }
        }
    }
    void ReadMono(float[] destination, int start, int count) {
        int channels = microphone.channels;
        int first = Math.Min(count, microphone.samples - start);
        ReadSection(destination, 0, start, first, channels);
        if (first < count) ReadSection(destination, first, 0, count - first, channels);
    }
    float[] interleaved;
    void ReadSection(float[] destination, int offset, int start, int count, int channels) {
        int length = count * channels;
        if (interleaved == null || interleaved.Length < length) interleaved = new float[GGWaveCodec.Frame * channels];
        microphone.GetData(interleaved, start);
        for (int i = 0; i < count; i++) {
            float sum = 0; for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
            destination[offset + i] = sum / channels;
        }
    }
    void OnApplicationPause(bool pause) {
        foreground = !pause;
        if (pause) { bool shouldResume = Listening || resumeListening; if (Transmitting) FailPlayback("Application paused"); StopListening(); resumeListening = shouldResume; }
        else if (pendingPermissionStart || (resumeListening && EchoSettings.AutoListen)) { pendingPermissionStart = false; StartListening(); }
    }
    void OnDestroy() { StopListening(); CleanupPlayback(); }
}
}

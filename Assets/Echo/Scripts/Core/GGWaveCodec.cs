using System;
using System.Text;

namespace Echo {
public sealed class GGWaveCodec : IDisposable {
    public const int MaxBytes = 140; // upstream kMaxLengthVariable
    public const int Frame = 1024;
    public static int ProtocolId(bool high, int speed) => (high ? 3 : 0) + Math.Max(0, Math.Min(2, speed));
    int tx = -1, rx = -1;
    readonly byte[] decoded = new byte[256];
    public GGWaveCodec(int captureRate, int playbackRate = 48000) {
        tx = GGWaveNative.echo_create(playbackRate, playbackRate, 4);
        rx = GGWaveNative.echo_create(captureRate, playbackRate, 2);
        if (tx < 0 || rx < 0 || GGWaveNative.echo_max_payload() != MaxBytes) { Dispose(); throw new InvalidOperationException("ggwave initialization failed"); }
    }
    public float[] Encode(string message, bool high, int speed, int volume) {
        byte[] data = Encoding.UTF8.GetBytes(message ?? "");
        if (data.Length < 1 || data.Length > MaxBytes) throw new ArgumentException("Message must contain 1–140 UTF-8 bytes");
        int protocol = ProtocolId(high, speed);
        int count = GGWaveNative.echo_encode_size(tx, data, data.Length, protocol, volume, IntPtr.Zero, 0);
        if (count <= 0 || count > 48000 * 60) throw new InvalidOperationException("ggwave encode size failed: " + count);
        var samples = new float[count];
        int written = GGWaveNative.echo_encode(tx, data, data.Length, protocol, volume, samples, count);
        if (written != count) throw new InvalidOperationException("ggwave encode failed: " + written);
        return samples;
    }
    public int Decode(float[] samples, int count, out string message) {
        message = null;
        int n = GGWaveNative.echo_decode(rx, samples, count, decoded, decoded.Length);
        if (n > 0) message = new UTF8Encoding(false, true).GetString(decoded, 0, n);
        return n;
    }
    public void Dispose() {
        if (tx >= 0) GGWaveNative.echo_destroy(tx);
        if (rx >= 0) GGWaveNative.echo_destroy(rx);
        tx = rx = -1;
    }
    public static string SelfTest() {
        using (var c = new GGWaveCodec(48000)) {
            string expected = "ECHO test ✓";
            var waveform = c.Encode(expected, false, 0, 25);
            var frame = new float[Frame];
            for (int offset = 0; offset < waveform.Length + 48000; offset += Frame) {
                Array.Clear(frame, 0, frame.Length);
                if (offset < waveform.Length) Array.Copy(waveform, offset, frame, 0, Math.Min(Frame, waveform.Length - offset));
                int result = c.Decode(frame, Frame, out string actual);
                if (result > 0) return actual == expected ? "PASS: in-memory ggwave round-trip" : "FAIL: unexpected decoded text";
            }
            return "FAIL: no packet decoded";
        }
    }
}
}

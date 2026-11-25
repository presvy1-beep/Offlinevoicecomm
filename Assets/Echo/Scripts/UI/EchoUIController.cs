using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Echo {
public sealed class EchoUIController : MonoBehaviour {
    static readonly Color Bg = Hex("0C0C0E"), Surface = Hex("202024"), Raised = Hex("303035"),
        TextColor = Hex("F4F4F5"), Muted = Hex("A1A1AA"), Green = Hex("4ADE80"), Red = Hex("F87171");
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
    Font font; Sprite rounded; Canvas canvas; RectTransform safe, historyContent;
    ScrollRect historyScroll; AcousticTransport transport; TransmissionHistory history;
    InputField composer; Button sendButton; GameObject settingsScreen, confirmScreen;
    Text status, sampleRate, listenLabel, byteCount, audibleLabel, highLabel, diagnosticResult;
    SignalGraph waveform, fft; bool high; float nextVisual;

    void Awake() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); rounded = MakeRoundedSprite();
        history = new TransmissionHistory(); transport = gameObject.AddComponent<AcousticTransport>();
        transport.Received += text => { history.Add(text, true); AddBubble(history.Items[history.Items.Count - 1]); ScrollBottom(); if (EchoSettings.Haptics) Handheld.Vibrate(); };
        transport.TransmissionCompleted += text => { history.Add(text, false); AddBubble(history.Items[history.Items.Count - 1]); ScrollBottom(); composer.text = ""; };
        transport.StatusChanged += SetStatus;
        Build(); foreach (var item in history.Items) AddBubble(item); ScrollBottom();
    }
    void Start() { if (EchoSettings.AutoListen) transport.StartListening(); }
    void Update() {
        FitSafeArea(); if (Time.unscaledTime < nextVisual) return; nextVisual = Time.unscaledTime + .05f;
        waveform.Refresh(); if (settingsScreen.activeSelf) fft.Refresh();
        waveform.color = transport.Listening ? Green : Muted;
        sampleRate.text = transport.CaptureRate > 0 ? (transport.CaptureRate / 1000f).ToString("0.#") + " kHz" : "— kHz";
        listenLabel.text = transport.Listening ? "Stop" : "Listen";
        RefreshComposer();
    }
    void FitSafeArea() {
        var r = Screen.safeArea; safe.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
        safe.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
        float keyboard = composer != null && composer.isFocused && TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0;
        if (composer != null && composer.isFocused && TouchScreenKeyboard.visible && keyboard < 10) keyboard = Screen.height * .35f;
        safe.offsetMin = new Vector2(0, keyboard / Mathf.Max(1, canvas.scaleFactor)); safe.offsetMax = Vector2.zero;
    }
    void SetStatus(string value) {
        if (status == null) return; status.text = "●  " + value;
        status.color = value.Contains("failed") || value.Contains("error") || value.Contains("denied") || value.Contains("unavailable") ? Red :
                       value == "Listening" || value == "Receiving" ? Green : Muted;
        Debug.Log("ECHO status: " + value);
        if (diagnosticResult != null && (value.StartsWith("Playback") || value.StartsWith("Test tone"))) diagnosticResult.text = value;
    }
    void Build() {
        var root = new GameObject("ECHO UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(440, 900); scaler.matchWidthOrHeight = .5f;
        safe = Panel(root.transform, "Safe area", Bg, false); Stretch(safe);
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var main = Stack(safe, 12, 8, 12, 8);
        var header = Row(main, 42); var wordmark = Label(header, "echo.", 26, TextColor); wordmark.fontStyle = FontStyle.Bold; FlexWidth(wordmark.rectTransform);
        Button(header, "⚙", ShowSettings, 44, 38);
        var statusRow = Row(main, 38); status = Label(statusRow, "●  Idle", 12, Muted); FlexWidth(status.rectTransform);
        sampleRate = Label(statusRow, "— kHz", 11, Muted); Width(sampleRate.rectTransform, 58);
        listenLabel = Button(statusRow, "Listen", () => { if (transport.Listening) transport.StopListening(); else transport.StartListening(); }, 74, 36).GetComponentInChildren<Text>();
        var monitor = Panel(main, "Waveform", Surface); Height(monitor, 82);
        waveform = new GameObject("Waveform", typeof(RectTransform), typeof(SignalGraph)).GetComponent<SignalGraph>();
        waveform.transform.SetParent(monitor, false); waveform.analyser = transport.Monitor; waveform.waveform = true; waveform.color = Muted; Stretch(waveform.rectTransform, 12);
        historyScroll = ScrollArea(main, out historyContent); FlexHeight((RectTransform)historyScroll.transform, 120);
        var historyLayout = historyContent.gameObject.AddComponent<VerticalLayoutGroup>(); historyLayout.spacing = 7;
        historyLayout.padding = new RectOffset(0, 0, 4, 4); historyLayout.childControlHeight = true; historyLayout.childForceExpandHeight = false;
        historyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var composerBox = Panel(main, "Composer", Surface); Height(composerBox, 122);
        var modes = Row(composerBox, 29); Anchor(modes, 8, 86, -8, 115);
        audibleLabel = Button(modes, "Audible", () => SetMode(false), 84, 29).GetComponentInChildren<Text>();
        highLabel = Button(modes, "High frequency · experimental", () => SetMode(true), 204, 29).GetComponentInChildren<Text>(); SetMode(false);
        var entry = Row(composerBox, 48); Anchor(entry, 8, 32, -8, 80);
        var inputBox = Panel(entry, "Message input", Raised); FlexWidth(inputBox); Height(inputBox, 48);
        composer = new GameObject("Message", typeof(RectTransform), typeof(Image), typeof(InputField)).GetComponent<InputField>();
        composer.transform.SetParent(inputBox, false); Stretch((RectTransform)composer.transform, 8);
        var inputHitTarget = composer.GetComponent<Image>();
        inputHitTarget.color = new Color(1, 1, 1, 0); inputHitTarget.raycastTarget = true;
        composer.targetGraphic = inputHitTarget; composer.interactable = true;
        composer.textComponent = Label(composer.transform, "", 15, TextColor); Stretch(composer.textComponent.rectTransform);
        var placeholder = Label(composer.transform, "Type a message...", 15, Muted); Stretch(placeholder.rectTransform); composer.placeholder = placeholder;
        composer.lineType = InputField.LineType.MultiLineSubmit; composer.characterLimit = 0;
        sendButton = Button(entry, "➤", Transmit, 48, 48);
        byteCount = Label(composerBox, "0 / 140 bytes", 11, Muted); Anchor(byteCount.rectTransform, 12, 4, -12, 27);
        composer.onValueChanged.AddListener(_ => RefreshComposer()); RefreshComposer();
        var footer = Label(main, "offline  ·  messages are not encrypted", 10, Muted); footer.alignment = TextAnchor.MiddleCenter; Height(footer.rectTransform, 17);
        settingsScreen = Panel(safe, "Settings", Bg, false).gameObject; Stretch((RectTransform)settingsScreen.transform); BuildSettings(); settingsScreen.SetActive(false);
        confirmScreen = Panel(safe, "Confirm", Bg, false).gameObject; Stretch((RectTransform)confirmScreen.transform); BuildConfirmation(); confirmScreen.SetActive(false);
    }
    void SetMode(bool value) { high = value; if (audibleLabel != null) { audibleLabel.color = high ? Muted : TextColor; highLabel.color = high ? TextColor : Muted; } }
    void RefreshComposer() {
        if (composer == null || byteCount == null || sendButton == null) return;
        int count = Encoding.UTF8.GetByteCount(composer.text);
        byteCount.text = count + " / " + GGWaveCodec.MaxBytes + " bytes";
        byteCount.color = count > GGWaveCodec.MaxBytes ? Red : Muted;
        sendButton.interactable = count > 0 && count <= GGWaveCodec.MaxBytes && !transport.Transmitting;
    }
    void Transmit() {
        int count = Encoding.UTF8.GetByteCount(composer.text);
        if (count < 1 || count > GGWaveCodec.MaxBytes || transport.Transmitting) return;
        try { transport.Transmit(composer.text, high, EchoSettings.Speed, Mathf.RoundToInt(EchoSettings.Volume)); }
        catch (Exception e) { SetStatus("Playback failed: " + e.Message); }
    }
    void AddBubble(EchoMessage message) {
        var row = new GameObject(message.incoming ? "Incoming" : "Outgoing", typeof(RectTransform)); row.transform.SetParent(historyContent, false);
        int lines = Mathf.Max(1, (message.text.Length + 36) / 37 + message.text.Split('\n').Length - 1);
        Height((RectTransform)row.transform, Mathf.Clamp(37 + lines * 18, 55, 135));
        var bubble = Panel(row.transform, "Bubble", message.incoming ? Surface : Raised);
        bubble.anchorMin = new Vector2(message.incoming ? 0 : .18f, 0); bubble.anchorMax = new Vector2(message.incoming ? .82f : 1, 1);
        bubble.offsetMin = new Vector2(2, 0); bubble.offsetMax = new Vector2(-2, 0);
        var text = Label(bubble, message.text, 14, TextColor); text.alignment = TextAnchor.UpperLeft;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(11, 24); text.rectTransform.offsetMax = new Vector2(-11, -8);
        var time = Label(bubble, (message.incoming ? "received" : "sent") + "  " + message.time, 10, Muted);
        Anchor(time.rectTransform, 11, 3, -54, 23);
        var copy = Button(bubble, "copy", () => { GUIUtility.systemCopyBuffer = message.text; SetStatus("Message copied"); }, 42, 24);
        copy.GetComponent<RectTransform>().anchorMin = copy.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        copy.GetComponent<RectTransform>().sizeDelta = new Vector2(42, 24);
        copy.GetComponent<RectTransform>().anchoredPosition = new Vector2(-25, 15);
    }
    void ScrollBottom() { Canvas.ForceUpdateCanvases(); if (historyScroll != null) historyScroll.verticalNormalizedPosition = 0; }
    void ShowSettings() { settingsScreen.SetActive(true); }
    void BuildSettings() {
        var stack = Stack(settingsScreen.transform, 14, 10, 14, 10);
        var head = Row(stack, 42); var title = Label(head, "Settings", 21, TextColor); FlexWidth(title.rectTransform);
        Button(head, "Done", () => settingsScreen.SetActive(false), 65, 38);
        var scroll = ScrollArea(stack, out var body); FlexHeight((RectTransform)scroll.transform, 100);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 7; layout.padding = new RectOffset(0, 0, 4, 12);
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Section(body, "Transmission");
        var modes = Row(body, 42); Button(modes, "Audible", () => SetMode(false), 100, 40);
        Button(modes, "High frequency · experimental", () => SetMode(true), 214, 40);
        var speed = Button(body, "Speed  ·  " + SpeedName(), null, 0, 42);
        speed.onClick.AddListener(() => { EchoSettings.Speed = (EchoSettings.Speed + 1) % 3; speed.GetComponentInChildren<Text>().text = "Speed  ·  " + SpeedName(); });
        var volumeLabel = Label(body, "Volume  ·  " + Mathf.RoundToInt(EchoSettings.Volume), 13, Muted);
        var slider = Slider(body); slider.onValueChanged.AddListener(v => { EchoSettings.Volume = v; volumeLabel.text = "Volume  ·  " + Mathf.RoundToInt(v); });
        Section(body, "Listening"); Toggle(body, "Automatic listening", () => EchoSettings.AutoListen, v => EchoSettings.AutoListen = v);
        Toggle(body, "Haptic feedback", () => EchoSettings.Haptics, v => EchoSettings.Haptics = v);
        Section(body, "Advanced signal monitor");
        var fftPanel = Panel(body, "Live FFT", Surface); Height(fftPanel, 110);
        fft = new GameObject("FFT", typeof(RectTransform), typeof(SignalGraph)).GetComponent<SignalGraph>();
        fft.transform.SetParent(fftPanel, false); fft.analyser = transport.Monitor; fft.color = TextColor;
        fft.rectTransform.anchorMin = Vector2.zero; fft.rectTransform.anchorMax = Vector2.one;
        fft.rectTransform.offsetMin = new Vector2(8, 23); fft.rectTransform.offsetMax = new Vector2(-8, -8);
        var axis = Label(fftPanel, "0                  6k                 12k                 24k Hz", 9, Muted); Anchor(axis.rectTransform, 10, 2, -10, 22);
        Section(body, "Diagnostics");
        Button(body, "Play quiet test tone", () => { try { transport.PlayTestTone(); diagnosticResult.text = "Playing a 440 Hz tone…"; } catch (Exception e) { diagnosticResult.text = "Audio test failed: " + e.Message; } }, 0, 42);
        Button(body, "Run ggwave codec round-trip", () => { try { diagnosticResult.text = GGWaveCodec.SelfTest(); } catch (Exception e) { diagnosticResult.text = "Codec test failed: " + e.Message; } }, 0, 42);
        diagnosticResult = Label(body, "The tone tests Unity playback; the codec test uses an in-memory waveform.", 12, Muted); Height(diagnosticResult.rectTransform, 50);
        Section(body, "Data & about"); Button(body, "Clear message history", () => confirmScreen.SetActive(true), 0, 42);
        var about = Label(body, "ECHO works offline. Acoustic messages are not encrypted and can be received nearby. High frequency mode is experimental.\n\nggwave © Georgi Gerganov · MIT license. See THIRD_PARTY_NOTICES.md in the project.", 12, Muted);
        Height(about.rectTransform, 125);
    }
    string SpeedName() => EchoSettings.Speed == 0 ? "Normal" : EchoSettings.Speed == 1 ? "Fast" : "Fastest";
    void BuildConfirmation() {
        var dialog = Panel(confirmScreen.transform, "Dialog", Surface); dialog.anchorMin = new Vector2(.08f, .38f); dialog.anchorMax = new Vector2(.92f, .60f); dialog.offsetMin = dialog.offsetMax = Vector2.zero;
        var text = Label(dialog, "Clear all messages?", 17, TextColor); Anchor(text.rectTransform, 16, 76, -16, 128);
        var actions = Row(dialog, 42); Anchor(actions, 16, 16, -16, 58);
        Button(actions, "Cancel", () => confirmScreen.SetActive(false), 110, 40);
        Button(actions, "Clear", () => { history.Clear(); foreach (Transform item in historyContent) Destroy(item.gameObject); confirmScreen.SetActive(false); }, 110, 40);
    }
    void Toggle(Transform parent, string label, Func<bool> get, Action<bool> set) {
        var button = Button(parent, label + (get() ? "  ·  On" : "  ·  Off"), null, 0, 42);
        button.onClick.AddListener(() => { set(!get()); button.GetComponentInChildren<Text>().text = label + (get() ? "  ·  On" : "  ·  Off"); });
    }
    void Section(Transform parent, string title) { var text = Label(parent, title.ToUpperInvariant(), 11, Muted); Height(text.rectTransform, 25); }
    Slider Slider(Transform parent) {
        var slider = new GameObject("Volume slider", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>(); slider.transform.SetParent(parent, false); Height((RectTransform)slider.transform, 40);
        var track = Panel(slider.transform, "Track", Raised); track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f); track.offsetMin = new Vector2(10, -3); track.offsetMax = new Vector2(-10, 3);
        var fill = Panel(slider.transform, "Fill", Muted); fill.anchorMin = new Vector2(0, .5f); fill.anchorMax = new Vector2(1, .5f); fill.offsetMin = new Vector2(10, -3); fill.offsetMax = new Vector2(-10, 3);
        var handle = Panel(slider.transform, "Handle", TextColor); handle.anchorMin = handle.anchorMax = new Vector2(0, .5f); handle.sizeDelta = new Vector2(18, 18);
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 1; slider.maxValue = 50; slider.value = EchoSettings.Volume; return slider;
    }
    ScrollRect ScrollArea(Transform parent, out RectTransform content) {
        var outer = new GameObject("Scroll area", typeof(RectTransform), typeof(ScrollRect)); outer.transform.SetParent(parent, false);
        var scroll = outer.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Panel(outer.transform, "Viewport", Bg, false); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>(); scroll.viewport = viewport;
        content = Panel(viewport, "Content", Bg, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero; scroll.content = content; return scroll;
    }
    Sprite MakeRoundedSprite() {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) {
            float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 8), dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 8);
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(8 - Mathf.Sqrt(dx * dx + dy * dy))));
        }
        texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }
    RectTransform Panel(Transform parent, string name, Color color, bool round = true) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; if (round) { image.sprite = rounded; image.type = Image.Type.Sliced; }
        return (RectTransform)go.transform;
    }
    Text Label(Transform parent, string value, int size, Color color) {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.alignment = TextAnchor.MiddleLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.raycastTarget = false; Height(text.rectTransform, size + 8); return text;
    }
    Button Button(Transform parent, string value, Action click, float width, float height) {
        var rect = Panel(parent, value, Raised); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        var label = Label(rect, value, 12, TextColor); label.alignment = TextAnchor.MiddleCenter; Stretch(label.rectTransform, 3);
        if (click != null) button.onClick.AddListener(() => click()); Height(rect, height); if (width > 0) Width(rect, width); return button;
    }
    Transform Stack(Transform parent, int left, int top, int right, int bottom) {
        var go = new GameObject("Vertical layout", typeof(RectTransform), typeof(VerticalLayoutGroup)); go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform);
        var layout = go.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(left, right, top, bottom); layout.spacing = 8;
        layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; return go.transform;
    }
    Transform Row(Transform parent, float height) {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup)); go.transform.SetParent(parent, false);
        var layout = go.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 7; layout.childControlWidth = true;
        layout.childForceExpandWidth = false; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        Height((RectTransform)go.transform, height); return go.transform;
    }
    static void Width(RectTransform rect, float value) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredWidth = e.minWidth = value; }
    static void Height(RectTransform rect, float value) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.preferredHeight = e.minHeight = value; }
    static void FlexWidth(RectTransform rect) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.flexibleWidth = 1; }
    static void FlexHeight(RectTransform rect, float minimum) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.flexibleHeight = 1; e.minHeight = minimum; }
    static void Stretch(RectTransform rect, float inset = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }
    static void Anchor(Transform item, float left, float bottom, float right, float top) { var rect = (RectTransform)item; rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(right, top); }
}
}

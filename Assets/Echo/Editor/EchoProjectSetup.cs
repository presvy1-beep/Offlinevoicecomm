using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Echo.Editor {
public static class EchoProjectSetup {
    [MenuItem("ECHO/Generate Main Scene and Configure Project")]
    public static void Configure() {
        const string scenePath = "Assets/Echo/Scenes/Echo.unity";
        Directory.CreateDirectory("Assets/Echo/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("ECHO Application", typeof(EchoUIController));
        new GameObject("ECHO Audio Listener", typeof(AudioListener));
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        PlayerSettings.productName = "ECHO";
        PlayerSettings.companyName = "ECHO";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.echo.acoustic");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.forceInternetPermission = false;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        ConfigurePlugin("Assets/Plugins/x86_64/echo_ggwave.dll", false);
        ConfigurePlugin("Assets/Plugins/Android/arm64-v8a/libecho_ggwave.so", true);
        GenerateIcon();
        AssetDatabase.SaveAssets();
        Debug.Log("ECHO project configured; scene saved at " + scenePath);
    }
    static void ConfigurePlugin(string path, bool android) {
        AssetDatabase.ImportAsset(path);
        var p = AssetImporter.GetAtPath(path) as PluginImporter;
        if (p == null) { Debug.LogError("Missing native plugin: " + path); return; }
        p.SetCompatibleWithAnyPlatform(false);
        p.SetCompatibleWithEditor(!android);
        if (!android) { p.SetEditorData("OS", "Windows"); p.SetEditorData("CPU", "x86_64"); }
        p.SetCompatibleWithPlatform(BuildTarget.Android, android);
        p.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, !android);
        p.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
        p.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
        if (android) p.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
        p.SaveAndReimport();
    }
    static void GenerateIcon() {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var dark = new Color32(9, 13, 20, 255); var blue = new Color32(56, 189, 248, 255);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
            float dx = x - 128, dy = y - 128, radius = Mathf.Sqrt(dx * dx + dy * dy);
            bool ring = (radius > 65 && radius < 70 && x < 182) || (radius > 92 && radius < 97 && x < 180);
            bool wave = Mathf.Abs(dx) < 57 && Mathf.Abs(dy) < (8 + 25 * Mathf.Abs(Mathf.Sin(dx * .085f))) && Mathf.Abs(dx % 18) < 4;
            texture.SetPixel(x, y, ring || wave ? blue : dark);
        }
        texture.Apply();
        Directory.CreateDirectory("Assets/Echo/UI");
        File.WriteAllBytes("Assets/Echo/UI/EchoIcon.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset("Assets/Echo/UI/EchoIcon.png");
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Echo/UI/EchoIcon.png");
        PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { icon }, IconKind.Any);
    }
    public static void BuildApk() {
        Configure();
        Directory.CreateDirectory("Build/Android");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Echo/Scenes/Echo.unity" }, locationPathName = "Build/Android/ECHO.apk", target = BuildTarget.Android, options = BuildOptions.None });
        Debug.Log("ECHO APK build: " + result.summary.result + " at Build/Android/ECHO.apk");
    }
    [MenuItem("ECHO/Run Editor Diagnostics")]
    public static void RunDiagnostics() {
        Debug.Log("ECHO codec diagnostic: " + GGWaveCodec.SelfTest());
        using (var codec = new GGWaveCodec(48000)) {
            var samples = codec.Encode("ECHO audio ✓", false, 0, 25);
            float peak = 0; double power = 0;
            foreach (float sample in samples) { peak = Mathf.Max(peak, Mathf.Abs(sample)); power += sample * sample; }
            if (peak < .001f || power / samples.Length < .00000001) throw new Exception("Encoded PCM is silent");
            var clip = AudioClip.Create("ECHO PCM test", samples.Length, 1, 48000, false);
            try {
                if (clip == null || !clip.SetData(samples, 0) || clip.samples != samples.Length || clip.channels != 1 ||
                    Mathf.Abs(clip.length - samples.Length / 48000f) > .001f) throw new Exception("AudioClip PCM mismatch");
                var check = new float[256];
                if (!clip.GetData(check, 0)) throw new Exception("AudioClip.GetData failed");
                for (int i = 0; i < check.Length; i++) if (Mathf.Abs(check[i] - samples[i]) > .0001f) throw new Exception("PCM changed in AudioClip");
                Debug.Log("ECHO PCM diagnostic: PASS samples=" + samples.Length + " duration=" + clip.length.ToString("F3") + " peak=" + peak.ToString("F4"));
            } finally { if (clip != null) UnityEngine.Object.DestroyImmediate(clip); }
        }
        var scene = EditorSceneManager.OpenScene("Assets/Echo/Scenes/Echo.unity", OpenSceneMode.Single);
        if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1) throw new Exception("Echo scene must contain one AudioListener");
        Debug.Log("ECHO scene diagnostic: PASS one AudioListener");
        string path = Path.Combine(Path.GetTempPath(), "echo-history-test-" + Guid.NewGuid() + ".json");
        try {
            var first = new TransmissionHistory(path);
            first.Add("Hello ✓", false);
            var second = new TransmissionHistory(path);
            if (second.Items.Count != 1 || second.Items[0].text != "Hello ✓") throw new Exception("History save/load failed");
            second.Clear();
            if (new TransmissionHistory(path).Items.Count != 0) throw new Exception("History clear failed");
            Debug.Log("ECHO history diagnostic: PASS save/load/clear");
        } finally { if (File.Exists(path)) File.Delete(path); }
    }
}
}

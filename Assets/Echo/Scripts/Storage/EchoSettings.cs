using UnityEngine;

namespace Echo {
public static class EchoSettings {
    public static float Volume { get => PlayerPrefs.GetFloat("echo.volume", 25); set { PlayerPrefs.SetFloat("echo.volume", Mathf.Clamp(value, 1, 50)); PlayerPrefs.Save(); } }
    public static int Speed { get => PlayerPrefs.GetInt("echo.speed", 0); set { PlayerPrefs.SetInt("echo.speed", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); } }
    public static bool Haptics { get => PlayerPrefs.GetInt("echo.haptics", 1) != 0; set { PlayerPrefs.SetInt("echo.haptics", value ? 1 : 0); PlayerPrefs.Save(); } }
    public static bool AutoListen { get => PlayerPrefs.GetInt("echo.listen", 1) != 0; set { PlayerPrefs.SetInt("echo.listen", value ? 1 : 0); PlayerPrefs.Save(); } }
}
}

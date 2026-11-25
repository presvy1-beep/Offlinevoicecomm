# ECHO — Offline Acoustic Communication

ECHO is an offline Android messaging application that transmits and receives text through sound waves using [ggwave](https://github.com/ggerganov/ggwave). It needs no Wi-Fi, Bluetooth, internet connection, account, or server.

## Features

- Audible and experimental high-frequency transmission modes
- Live waveform, local message history, UTF-8 byte counter, and listening controls
- Minimal monochrome messaging interface
- Android ARM64 support; up to 140 UTF-8 bytes per message

## How it works

```text
Text → UTF-8 → ggwave encoder → Unity audio playback → speaker
Microphone → PCM samples → ggwave decoder → text → local history
```

Two nearby devices can communicate through their speakers and microphones. Open ECHO on both devices, grant microphone permission, start listening, enter a message on one device, and tap Send. Start with Audible mode and moderate volume. The [ggwave Waver website](https://waver.ggerganov.com/) can be used as a compatible endpoint when its protocol settings match ECHO's audible mode. Local playback completion does not guarantee that another device received the message.

## Download and install

Download [ECHO.apk](https://github.com/presvy1-beep/Offlinevoicecomm/releases/download/v1.0.0/ECHO.apk) from the [v1.0.0 release](https://github.com/presvy1-beep/Offlinevoicecomm/releases/tag/v1.0.0). Install it on an Android ARM64 device, allow installation from your chosen source if Android asks, then grant microphone permission. The package identifier is `com.echo.acoustic`.

## Build from source

Clone this repository and open it in **Unity 6000.6.8f1** with Android Build Support. The main scene is `Assets/Echo/Scenes/Echo.unity`; Unity project files are in `Assets/`, `Packages/`, and `ProjectSettings/`. The required Windows and Android native plugins are already included in `Assets/Plugins/`.

For a native rebuild, install CMake and Visual Studio C++ Build Tools. The Android script uses the NDK installed with Unity 6000.6.8f1. Run from PowerShell at the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Build-Native-Windows.ps1
powershell -ExecutionPolicy Bypass -File Tools/Build-Native-Android.ps1
powershell -ExecutionPolicy Bypass -File Tools/Test-Native.ps1
```

The scripts compile the vendored `ThirdParty/ggwave` source and place plugins in `Assets/Plugins/`. In Unity, use **ECHO → Generate Main Scene and Configure Project**, then build for Android. A batch build is available through `Echo.Editor.EchoProjectSetup.BuildApk`; it writes `Build/Android/ECHO.apk`.

## Dependency and credits

`ThirdParty/ggwave` vendors the library source from upstream commit [`060aec73dd7123ccac200442f75bdc7369795ffe`](https://github.com/ggerganov/ggwave/commit/060aec73dd7123ccac200442f75bdc7369795ffe). Upstream examples, media, and submodule references are omitted because ECHO's native build uses `include/` and `src/`. The ggwave MIT license is preserved in `ThirdParty/ggwave/LICENSE` and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). No license is assigned here to ECHO's original code.

## Privacy and limitations

ECHO does not require network access. Messages are **not encrypted** and may be received by nearby compatible devices. Message history is stored locally on the device; raw microphone recordings are not stored. Acoustic reliability depends on noise, distance, speaker and microphone hardware, and volume. High-frequency mode is experimental and may be audible or unsupported on some devices.

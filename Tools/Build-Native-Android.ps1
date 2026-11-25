$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$editors = 'C:\Program Files\Unity\Hub\Editor'
$version = (Select-String -Path (Join-Path $root 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$ndk = Join-Path $editors "$version\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
if (!(Test-Path (Join-Path $ndk 'build\cmake\android.toolchain.cmake'))) { throw "Unity Android NDK missing: $ndk" }
$build = Join-Path $root 'Build\Native\Android'
cmake -S (Join-Path $root 'Native') -B $build -G Ninja -DCMAKE_TOOLCHAIN_FILE="$ndk\build\cmake\android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-23 -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE) { throw 'CMake configure failed' }
cmake --build $build --config Release
if ($LASTEXITCODE) { throw 'Native build failed' }
$dest = Join-Path $root 'Assets\Plugins\Android\arm64-v8a'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $build 'libecho_ggwave.so') $dest -Force

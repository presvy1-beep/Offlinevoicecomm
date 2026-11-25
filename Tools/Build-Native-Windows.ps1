$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$build = Join-Path $root 'Build\Native\Windows'
cmake -S (Join-Path $root 'Native') -B $build -A x64 -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE) { throw 'CMake configure failed; install Visual Studio C++ build tools' }
cmake --build $build --config Release
if ($LASTEXITCODE) { throw 'Native build failed' }
$dest = Join-Path $root 'Assets\Plugins\x86_64'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $build 'Release\echo_ggwave.dll') $dest -Force

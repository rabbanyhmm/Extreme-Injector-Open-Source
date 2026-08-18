# Build-NativeCore.ps1
# Builds InjectorCore32.dll (x86) and InjectorCore64.dll (x64)
# Outputs directly to src\ExtremeInjectorReplica\Resources\
# No external dependencies beyond MSVC + Windows SDK

$ErrorActionPreference = "Stop"

# -- Locate MSBuild from VS2022
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found at: $msbuild"
    exit 1
}

$proj = "$PSScriptRoot\src\ExtremeInjector.NativeCore\ExtremeInjector.NativeCore.vcxproj"

Write-Host "Building InjectorCore32.dll (Win32)..." -ForegroundColor Cyan
& $msbuild $proj /p:Platform=Win32 /p:Configuration=Release /p:SolutionDir="$PSScriptRoot\" /nologo /m /v:m
if ($LASTEXITCODE -ne 0) { Write-Error "Win32 build failed"; exit 1 }

Write-Host "Building InjectorCore64.dll (x64)..." -ForegroundColor Cyan
& $msbuild $proj /p:Platform=x64 /p:Configuration=Release /p:SolutionDir="$PSScriptRoot\" /nologo /m /v:m
if ($LASTEXITCODE -ne 0) { Write-Error "x64 build failed"; exit 1 }

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "  -> src\ExtremeInjectorReplica\Resources\InjectorCore32.dll"
Write-Host "  -> src\ExtremeInjectorReplica\Resources\InjectorCore64.dll"

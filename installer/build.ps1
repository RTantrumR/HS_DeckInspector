# Builds installer/out/HSDeckInspector-Setup-<version>.exe and, beside it, the plugin DLL for Firestone's mods page.
# Both go on the release. Firestone downloads <repo>/releases/latest/download/com.tantrumua.deckinspector.dll, so the DLL
# keeps exactly that name, and the release's tag is the plain version (1.0.0): it compares versions with parseInt,
# which a v prefix turns into NaN, so no update would ever show.
#   powershell -ExecutionPolicy Bypass -File installer\build.ps1 [-HsDir E:\Games\Hearthstone]
# Builds the plugin, stages BepInEx (taken from a Hearthstone folder where it already runs) and the plugin into
# installer/stage, then compiles HSDeckInspector.iss with Inno Setup 6.
param(
    [string]$HsDir = "E:\Games\Hearthstone"
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$repo = Split-Path $here -Parent
$stage = Join-Path $here "stage"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found (winget install JRSoftware.InnoSetup)" }

$version = ([xml](Get-Content (Join-Path $repo "plugin\HsDeckInspector.csproj"))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1

# Built into the staging folder, so a running Hearthstone (which locks the installed DLL) doesn't matter.
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
$plug = New-Item -ItemType Directory -Force (Join-Path $stage "plugin")
dotnet build (Join-Path $repo "plugin") -c Release --no-incremental "-p:OutputPath=$plug\" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "plugin build failed" }
Get-ChildItem $plug -Exclude "com.tantrumua.deckinspector.dll" | Remove-Item -Recurse -Force

# BepInEx 5.4.23.5 + doorstop 4.5.0 x64 (older pairs crash Unity 6); HS loads the proxy as version.dll.
$bep = New-Item -ItemType Directory -Force (Join-Path $stage "bepinex")
foreach ($f in "winhttp.dll", "version.dll", "doorstop_config.ini", ".doorstop_version") {
    Copy-Item (Join-Path $HsDir $f) $bep
}
foreach ($d in "core", "unstripped_corlib") {
    Copy-Item -Recurse (Join-Path $HsDir "BepInEx\$d") (Join-Path $bep "BepInEx\$d")
}
New-Item -ItemType Directory -Force (Join-Path $bep "BepInEx\config") | Out-Null
Copy-Item (Join-Path $HsDir "BepInEx\config\BepInEx.cfg") (Join-Path $bep "BepInEx\config")
Get-ChildItem -Recurse $bep -Filter *.xml | Remove-Item

& $iscc "/DAppVersion=$version" (Join-Path $here "HSDeckInspector.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
Copy-Item (Join-Path $plug "com.tantrumua.deckinspector.dll") (Join-Path $here "out") -Force
Get-Item (Join-Path $here "out\HSDeckInspector-Setup-$version.exe"), (Join-Path $here "out\com.tantrumua.deckinspector.dll")

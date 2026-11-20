# Builds the desktop preview of the dashboard and renders it to a PNG.
# Usage: powershell -File tools\UiPreview\preview.ps1 [-Out <path.png>]
param([string]$Out = "$PSScriptRoot\bin\dashboard.png")

$ErrorActionPreference = 'Stop'
$app = Resolve-Path "$PSScriptRoot\..\..\FEZPortalHelloWorld"
$bin = "$PSScriptRoot\bin"
New-Item -ItemType Directory -Force $bin | Out-Null

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
$csc = Join-Path (Split-Path $msbuild) "Roslyn\csc.exe"

# Shared UI code from the app, minus the device-only primitives.
$shared = Get-ChildItem "$app\Ui\*.cs" | Where-Object { $_.Name -ne 'Canvas.Device.cs' } | ForEach-Object { $_.FullName }

& $csc /nologo /langversion:latest /target:exe /out:"$bin\UiPreview.exe" /r:System.Drawing.dll `
    "$PSScriptRoot\Program.cs" "$PSScriptRoot\Canvas.Desktop.cs" @shared
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$bin\UiPreview.exe" "$app\Resources" $Out
exit $LASTEXITCODE

# Builds the desktop preview of Holiday Lights, renders its pages to PNGs and checks the strip bytes.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File FEZPortalHolidayLights\Preview\preview.ps1
# Output: FEZPortalHolidayLights\Preview\bin\*.png (and *.2x.png zoomed copies)

$ErrorActionPreference = 'Stop'
$app = Resolve-Path "$PSScriptRoot\.."
$bin = "$PSScriptRoot\bin"
New-Item -ItemType Directory -Force $bin | Out-Null

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
$csc = Join-Path (Split-Path $msbuild) "Roslyn\csc.exe"

# Shared code from the app: pages, lights and UI, minus the device-only parts.
$shared = @(Get-ChildItem "$app\App\*.cs", "$app\Lights\*.cs", "$app\Ui\*.cs" |
    Where-Object { $_.Name -ne 'Canvas.Device.cs' } | ForEach-Object { $_.FullName })

& $csc /nologo /langversion:latest /target:exe /out:"$bin\LightsPreview.exe" /r:System.Drawing.dll `
    "$PSScriptRoot\Program.cs" "$PSScriptRoot\Canvas.Desktop.cs" @shared
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$bin\LightsPreview.exe" "$app\Resources" "$app\SdCard\HolidayLights" $bin
exit $LASTEXITCODE

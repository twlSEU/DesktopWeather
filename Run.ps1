$ErrorActionPreference = 'Stop'
$taskRelease = Join-Path $PSScriptRoot 'release\DesktopWeather.exe'
if (Test-Path -LiteralPath $taskRelease) {
    Start-Process -FilePath $taskRelease -WindowStyle Normal
    exit
}
$taskSdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $taskSdk) { $env:DOTNET_ROOT = Split-Path -Parent $taskSdk }
& (Join-Path $PSScriptRoot 'Build.ps1')
Start-Process -FilePath (Join-Path $PSScriptRoot 'bin\Debug\net10.0-windows\DesktopWeather.exe') -WindowStyle Normal

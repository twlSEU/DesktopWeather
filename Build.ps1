param([switch]$Publish, [switch]$Test)
$ErrorActionPreference = 'Stop'
$taskSdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskSdk)) {
    $taskCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $taskCommand) { throw 'Please install the .NET 10 SDK, or place it in .tools\dotnet.' }
    $taskSdk = $taskCommand.Source
}
$env:DOTNET_ROOT = Split-Path -Parent $taskSdk
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\nuget'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
Push-Location $PSScriptRoot
try {
    if ($Test) {
        & $taskSdk run --project 'tests\CoreChecks.csproj' -c Release -- (Join-Path $PSScriptRoot '.tools\test-artifacts\core')
        if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    }
    if ($Publish) {
        & $taskSdk publish 'DesktopWeather.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o 'release'
    } else {
        & $taskSdk build 'DesktopWeather.csproj' -c Debug
    }
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Test) {
        $taskApp = if ($Publish) { Join-Path $PSScriptRoot 'release\DesktopWeather.exe' } else { Join-Path $PSScriptRoot 'bin\Debug\net10.0-windows\DesktopWeather.exe' }
        $taskArtifacts = Join-Path $PSScriptRoot '.tools\test-artifacts\ui'
        $taskProcess = Start-Process -FilePath $taskApp -ArgumentList @('--self-test', $taskArtifacts) -WindowStyle Hidden -PassThru
        if (-not $taskProcess.WaitForExit(30000)) { $taskProcess.Kill(); throw 'UI checks timed out.' }
        if ($taskProcess.ExitCode -ne 0) { throw "UI checks failed. See $taskArtifacts\self-test.json" }
        Write-Host "UI checks passed. See $taskArtifacts\self-test.json"
    }
} finally { Pop-Location }

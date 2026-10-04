$taskOriginalRoot = $env:DOTNET_ROOT
$taskOriginalCliHome = $env:DOTNET_CLI_HOME
$taskOriginalNuget = $env:NUGET_PACKAGES
$taskOriginalTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$taskExitCode = 1
try {
    $env:DOTNET_ROOT = Join-Path $PSScriptRoot '.tools\dotnet'
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli-home'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') @args
    $taskExitCode = $LASTEXITCODE
}
finally {
    $env:DOTNET_ROOT = $taskOriginalRoot
    $env:DOTNET_CLI_HOME = $taskOriginalCliHome
    $env:NUGET_PACKAGES = $taskOriginalNuget
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $taskOriginalTelemetry
}
exit $taskExitCode

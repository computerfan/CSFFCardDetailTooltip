param(
    [Parameter(Mandatory)][string]$Username,
    [string]$DotNetPath = ''
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -eq 'true') { throw 'Run this interactive setup locally.' }
$repo = Split-Path $PSScriptRoot -Parent
$dotnet = & "$PSScriptRoot\Get-DotNetSdk.ps1" -DotNetPath $DotNetPath
Write-Host "Using .NET SDK: $dotnet"
$output = Join-Path $repo 'obj\steam-auth'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$downloader = & "$PSScriptRoot\Get-DepotDownloader.ps1"
& $dotnet build "$PSScriptRoot\SteamSession\SteamSession.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Steam session helper build failed.' }
$previous = @{}
foreach ($name in @('STEAM_USERNAME', 'STEAM_DOWNLOAD_DIR', 'STEAM_BRANCH')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:STEAM_USERNAME = $Username
    $env:STEAM_DOWNLOAD_DIR = Join-Path $output 'manifest'
    $env:STEAM_BRANCH = 'openbetabranch'
    & $dotnet "$PSScriptRoot\SteamSession\bin\Release\net10.0\SteamSession.dll" login $downloader (Join-Path $output 'session.b64')
    if ($LASTEXITCODE -ne 0) { throw 'Steam login failed. No usable session was exported.' }
    Write-Host 'Set STEAM_USERNAME and STEAM_SESSION_B64 in the steam-build GitHub environment (see docs/steam-build.md).'
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}

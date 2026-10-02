# Offline orchestration test: a fake downloader supplies fixture DLLs and a manifest.
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot "../obj/steam-download-test-$([Guid]::NewGuid().ToString('N'))"
$saved = @{}
foreach ($name in @('STEAM_USERNAME', 'STEAM_SESSION_B64', 'GITHUB_OUTPUT', 'STEAM_DOWNLOAD_DIR', 'STEAM_BRANCH', 'STEAM_MANIFEST', 'STEAM_FILE_LIST')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
function dotnet {
    $regex = (Get-Content -LiteralPath $env:STEAM_FILE_LIST -Raw).Trim().Substring(6)
    foreach ($path in @('Card Survival - Fantasy Forest_Data/Managed/Assembly-CSharp.dll', 'Card Survival - Fantasy Forest_Data\Managed\UnityEngine.dll')) {
        if ($path -notmatch $regex) { throw 'Managed DLL excluded by file filter.' }
    }
    foreach ($path in @('Card Survival - Fantasy Forest.exe', 'Card Survival - Fantasy Forest_Data/Managed/nested/other.dll', 'Card Survival - Fantasy Forest_Data/Managed/readme.txt')) {
        if ($path -match $regex) { throw 'Non-managed content included by file filter.' }
    }
    $managed = Join-Path $env:STEAM_DOWNLOAD_DIR 'Card Survival - Fantasy Forest_Data/Managed'
    New-Item -ItemType Directory -Path $managed | Out-Null
    foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.UI.dll', 'Unity.TextMeshPro.dll')) {
        Set-Content -LiteralPath (Join-Path $managed $name) -Value 'fixture only'
    }
    Set-Content -LiteralPath (Join-Path $env:STEAM_DOWNLOAD_DIR '2868861_123456.manifest') -Value 'fixture only'
    $global:LASTEXITCODE = 0
}
try {
    $env:STEAM_USERNAME = 'fixture'
    $env:STEAM_SESSION_B64 = 'fixture-not-a-credential'
    $env:GITHUB_OUTPUT = ''
    & "$PSScriptRoot/Get-SteamGameAssemblies.ps1" -Destination "$root/latest" -DownloaderPath fixture
    $info = Get-Content "$root/latest/game-build.json" -Raw | ConvertFrom-Json
    $hash = (Get-FileHash "$root/latest/Card Survival - Fantasy Forest_Data/Managed/Assembly-CSharp.dll").Hash
    if ($info.manifestId -ne '123456' -or $info.branch -ne 'openbetabranch' -or $info.assemblySha256 -ne $hash) { throw 'Incorrect provenance.' }
    if ([string]$env:STEAM_DOWNLOAD_DIR -ne [string]$saved.STEAM_DOWNLOAD_DIR) { throw 'Download environment was not restored.' }
    $rejected = $false
    try { & "$PSScriptRoot/Get-SteamGameAssemblies.ps1" -Destination "$root/latest" -DownloaderPath fixture }
    catch { $rejected = $true }
    if (!$rejected) { throw 'Stale download directory accepted.' }
    $rejected = $false
    try { & "$PSScriptRoot/Get-SteamGameAssemblies.ps1" -Manifest 789 -Destination "$root/wrong-pin" -DownloaderPath fixture }
    catch { $rejected = $true }
    if (!$rejected -or (Test-Path "$root/wrong-pin/game-build.json")) { throw 'Mismatched manifest accepted.' }
    $env:STEAM_SESSION_B64 = ''
    $rejected = $false
    try { & "$PSScriptRoot/Get-SteamGameAssemblies.ps1" -Destination "$root/no-auth" -DownloaderPath fixture }
    catch { $rejected = $true }
    if (!$rejected -or (Test-Path "$root/no-auth")) { throw 'Missing credentials accepted.' }
    Write-Host 'PASS: Managed-file filter, provenance, stale directory, manifest mismatch and missing authentication.'
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    # All fixtures remain in the ignored obj directory for inspection.
}

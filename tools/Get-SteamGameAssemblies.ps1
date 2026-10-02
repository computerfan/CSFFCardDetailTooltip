param(
    [ValidateSet('openbetabranch', 'public')][string]$Branch = 'openbetabranch',
    [ValidatePattern('^$|^[1-9][0-9]{0,19}$')][string]$Manifest = '',
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$DownloaderPath
)
$ErrorActionPreference = 'Stop'
if (!$env:STEAM_USERNAME -or !$env:STEAM_SESSION_B64) { throw 'Configure STEAM_USERNAME and STEAM_SESSION_B64 in the steam-build environment. See docs/steam-build.md.' }
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) { throw 'Use a fresh download directory so stale DLLs/manifests cannot be selected.' }
New-Item -ItemType Directory -Path $destinationPath | Out-Null
$fileList = Join-Path $destinationPath 'managed-files.txt'
Set-Content -LiteralPath $fileList -Encoding utf8NoBOM -Value 'regex:^Card Survival - Fantasy Forest_Data[\\/]Managed[\\/][^\\/]+\.dll$'
$previous = @{}
foreach ($name in @('STEAM_DOWNLOAD_DIR', 'STEAM_BRANCH', 'STEAM_MANIFEST', 'STEAM_FILE_LIST')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:STEAM_DOWNLOAD_DIR = $destinationPath
    $env:STEAM_BRANCH = $Branch
    $env:STEAM_MANIFEST = $Manifest
    $env:STEAM_FILE_LIST = $fileList
    dotnet "$PSScriptRoot\SteamSession\bin\Release\net10.0\SteamSession.dll" download $DownloaderPath
    if ($LASTEXITCODE -ne 0) { throw 'Steam download failed; check account ownership/session expiry.' }
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
$managed = Join-Path $destinationPath 'Card Survival - Fantasy Forest_Data\Managed'
foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.UI.dll', 'Unity.TextMeshPro.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $managed $name))) { throw "Steam download is missing $name." }
}
$manifests = @(Get-ChildItem -LiteralPath $destinationPath -Recurse -File -Filter '2868861_*.manifest')
if ($manifests.Count -ne 1 -or $manifests[0].Name -notmatch '^2868861_(\d+)\.manifest$') { throw 'Expected exactly one Steam depot manifest.' }
$resolvedManifest = $Matches[1]
if ($Manifest -and $resolvedManifest -ne $Manifest) { throw 'Downloaded manifest does not match the requested pin.' }
$provenance = [ordered]@{
    appId = 2868860; depotId = 2868861; branch = $Branch; manifestId = $resolvedManifest
    selection = $(if ($Manifest) { 'pinned manifest' } else { 'latest branch manifest' })
    assemblySha256 = (Get-FileHash -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll')).Hash
    downloadedAtUtc = [DateTime]::UtcNow.ToString('O'); depotDownloaderVersion = '3.4.0'
}
$provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destinationPath 'game-build.json') -Encoding utf8NoBOM
if ($env:GITHUB_OUTPUT) { "managed=$managed" | Add-Content -LiteralPath $env:GITHUB_OUTPUT }
Write-Host "Downloaded depot 2868861 manifest $resolvedManifest; assembly SHA256 $($provenance.assemblySha256)."

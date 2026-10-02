$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$errors)
    if ($errors.Count) { throw "PowerShell syntax errors in $($file.Name): $errors" }
}
$project = [xml](Get-Content (Join-Path $repo 'CSFFCardDetailTooltip.csproj') -Raw)
$expected = [string](@($project.Project.PropertyGroup.Version | Where-Object { $_ })[0])
$version = & "$PSScriptRoot/Get-BuildVersion.ps1" -RefType branch -RefName master -RunNumber 17
if ($version.PluginVersion -ne $expected -or $version.PackageVersion -ne "$expected-ci.17") { throw 'Branch version mismatch.' }
$version = & "$PSScriptRoot/Get-BuildVersion.ps1" -RefType tag -RefName "v$expected"
if ($version.PackageVersion -ne $expected) { throw 'Release version mismatch.' }
$rejected = $false
try { & "$PSScriptRoot/Get-BuildVersion.ps1" -RefType tag -RefName 'v0.0.0' | Out-Null }
catch { $rejected = $true }
if (!$rejected) { throw 'Mismatched release tag was accepted.' }
Write-Host 'PASS: PowerShell syntax, branch/release versions and mismatched tag rejection.'
& "$PSScriptRoot/Test-SteamDownload.ps1"
$dotnet = & "$PSScriptRoot/Get-DotNetSdk.ps1"
& $dotnet build "$PSScriptRoot/SteamSession/SteamSession.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Steam session helper build failed.' }
$downloader = & "$PSScriptRoot/Get-DepotDownloader.ps1"
& $dotnet "$PSScriptRoot/SteamSession/bin/Release/net10.0/SteamSession.dll" self-test $downloader
if ($LASTEXITCODE -ne 0) { throw 'Steam session serialization test failed.' }

param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\CSFFCardDetailTooltip.csproj'),
    [string]$RefType = $env:GITHUB_REF_TYPE,
    [string]$RefName = $env:GITHUB_REF_NAME,
    [string]$RunNumber = $env:GITHUB_RUN_NUMBER
)
$ErrorActionPreference = 'Stop'
$project = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
$versions = @($project.Project.PropertyGroup.Version | Where-Object { $_ })
if ($versions.Count -ne 1 -or $versions[0] -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected one numeric project Version.' }
$pluginVersion = [string]$versions[0]
if ($RefType -eq 'tag') {
    if ($RefName -cne "v$pluginVersion") { throw "Release tag must equal v$pluginVersion from the project." }
    $packageVersion = $pluginVersion
}
else {
    if (!$RunNumber) { $RunNumber = '0' }
    if ($RunNumber -notmatch '^\d+$') { throw 'Invalid run number.' }
    $packageVersion = "$pluginVersion-ci.$RunNumber"
}
[pscustomobject]@{ PluginVersion = $pluginVersion; PackageVersion = $packageVersion }

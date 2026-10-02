param([string]$DotNetPath = '')
$ErrorActionPreference = 'Stop'
$candidates = @()
if ($DotNetPath) {
    $candidates += $DotNetPath
}
else {
    $systemDotNet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    if ($systemDotNet) { $candidates += $systemDotNet.Source }
    $candidates += Join-Path $PSScriptRoot '../obj/steam-tools/dotnet/dotnet.exe'
}
foreach ($candidate in $candidates) {
    if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
    $versionText = & $candidate --version 2>$null
    $version = $null
    if ($LASTEXITCODE -eq 0 -and [version]::TryParse([string]$versionText, [ref]$version) -and $version.Major -eq 10) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
}
throw 'The Steam session helper requires the .NET 10 SDK. Install it and reopen PowerShell, or pass -DotNetPath with the full path to a .NET 10 SDK dotnet.exe. A runtime-only installation is insufficient. See docs/steam-build.md.'

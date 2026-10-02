$ErrorActionPreference = 'Stop'
$sdk = & "$PSScriptRoot/Get-DotNetSdk.ps1"
$fixture = Join-Path $PSScriptRoot "../obj/sdk-probe-test-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixture | Out-Null
# A dotnet host without its host/fxr directory emits native stderr and exits.
$brokenHost = Join-Path $fixture 'dotnet.exe'
Copy-Item -LiteralPath $sdk -Destination $brokenHost
$previousPath = $env:PATH
try {
    $env:PATH = "$fixture;$previousPath"
    $resolved = & "$PSScriptRoot/Get-DotNetSdk.ps1"
    if ($resolved -ne $sdk) { throw 'A failed host prevented selection of the working SDK.' }
    $rejected = $false
    try { & "$PSScriptRoot/Get-DotNetSdk.ps1" -DotNetPath $brokenHost | Out-Null }
    catch {
        if ($_.Exception.Message -notlike '*requires the .NET 10 SDK*') { throw }
        $rejected = $true
    }
    if (!$rejected) { throw 'Broken SDK host accepted.' }
    Write-Host "PASS: Failed native SDK probe, fallback and setup guidance (PowerShell $($PSVersionTable.PSVersion))."
}
finally { $env:PATH = $previousPath }

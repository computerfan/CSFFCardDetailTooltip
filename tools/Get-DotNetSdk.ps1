param([string]$DotNetPath = '')
$ErrorActionPreference = 'Stop'
$candidates = @()
if ($DotNetPath) {
    $candidates += $DotNetPath
}
else {
    $systemDotNet = Get-Command dotnet -All -CommandType Application -ErrorAction SilentlyContinue
    if ($systemDotNet) { $candidates += $systemDotNet.Source }
    $candidates += Join-Path $PSScriptRoot '../obj/steam-tools/dotnet/dotnet.exe'
}
foreach ($candidate in $candidates) {
    if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
    # Windows PowerShell turns redirected native stderr into terminating errors
    # under ErrorActionPreference=Stop, even for an expected failed SDK probe.
    $process = New-Object System.Diagnostics.Process
    try {
        $process.StartInfo.FileName = (Resolve-Path -LiteralPath $candidate).Path
        $process.StartInfo.Arguments = '--version'
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(10000)) {
            $process.Kill()
            continue
        }
        $versionText = $stdout.GetAwaiter().GetResult().Trim()
        [void]$stderr.GetAwaiter().GetResult()
        $version = $null
        if ($process.ExitCode -eq 0 -and [version]::TryParse($versionText, [ref]$version) -and $version.Major -eq 10) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    catch { continue }
    finally { $process.Dispose() }
}
throw 'The Steam session helper requires the .NET 10 SDK. Install it and reopen PowerShell, or pass -DotNetPath with the full path to a .NET 10 SDK dotnet.exe. A runtime-only installation is insufficient. See docs/steam-build.md.'

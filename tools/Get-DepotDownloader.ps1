param([string]$Destination = (Join-Path $PSScriptRoot '..\obj\steam-tools\depotdownloader'))
$ErrorActionPreference = 'Stop'
$version = '3.4.0'
$expected = '7419F65EFB7EB16B6E56987ED1B76475F29C02475C0016166C84798388E796BC'
$destinationPath = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
$archive = Join-Path $destinationPath 'DepotDownloader-framework.zip'
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive).Hash -ne $expected) {
    Invoke-WebRequest "https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_$version/DepotDownloader-framework.zip" -OutFile $archive -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $archive).Hash -ne $expected) { throw 'DepotDownloader checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destinationPath -Force
Join-Path $destinationPath 'DepotDownloader.dll'

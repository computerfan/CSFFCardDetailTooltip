param(
    [Parameter(Mandatory)][ValidatePattern('^v\d+\.\d+\.\d+$')][string]$Tag,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
git rev-parse --verify "refs/tags/$Tag^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Release tag does not exist.' }
$previous = git describe --tags --abbrev=0 --match 'v[0-9]*' "$Tag^" 2>$null
$range = if ($LASTEXITCODE -eq 0) { "$previous..$Tag" } else { $Tag }
$history = @(git log --reverse '--format=- %h %s' $range --)
if ($LASTEXITCODE -ne 0 -or !$history.Count) { throw 'Cannot generate commit history.' }
# Release descriptions contain only commit history, without generated summaries.
$history | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

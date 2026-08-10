# Injects a random leading emoji, always different from the previous commit's emoji.
param(
    [Parameter(Mandatory = $true)]
    [string]$MessageFile,

    [Parameter(Mandatory = $false)]
    [string]$Source = ''
)

$ErrorActionPreference = 'Stop'

if ($Source -in @('merge', 'squash')) {
    exit 0
}

. "$PSScriptRoot\commit-emoji-lib.ps1"

$path = Get-CommitMessagePath -MessageFile $MessageFile
$lines = [System.Collections.Generic.List[string]]::new()
$lines.AddRange([string[]][System.IO.File]::ReadAllLines($path, $script:Utf8))

$idx = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    $candidate = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
    if ($candidate.StartsWith('#')) { continue }
    $idx = $i
    break
}

if ($idx -lt 0) {
    exit 0
}

$firstLine = $lines[$idx]
if ($firstLine -match '^(Merge |Revert )') {
    exit 0
}

$lastEmoji = Get-LastCommitEmoji
$emoji = New-RandomCommitEmoji -Exclude $lastEmoji
$withoutEmoji = Remove-LeadingEmoji -Text $firstLine
$lines[$idx] = "$emoji $withoutEmoji"

# Strip forbidden trailers (IDE / tooling may inject them).
$trailerPattern = '^(Signed-off-by|Co-authored-by|Reviewed-by|Acked-by)\s*:'
$cleaned = [System.Collections.Generic.List[string]]::new()
foreach ($line in $lines) {
    if ($line -match $trailerPattern) { continue }
    $cleaned.Add($line)
}

[System.IO.File]::WriteAllLines($path, $cleaned, $script:Utf8)
exit 0

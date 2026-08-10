# Validates: <random-emoji> <type>(<scope>)?!?: <subject>
# Emoji must differ from the previous commit's leading emoji. No trailers.
param(
    [Parameter(Mandatory = $true)]
    [string]$MessageFile
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\commit-emoji-lib.ps1"

$path = Get-CommitMessagePath -MessageFile $MessageFile
$lines = [System.IO.File]::ReadAllLines($path, $script:Utf8)
if ($lines.Length -eq 0 -or [string]::IsNullOrWhiteSpace($lines[0])) {
    Write-Error 'Commit message vide.'
    exit 1
}
$firstLine = $lines[0]

if ($firstLine -match '^(Merge |Revert )') {
    exit 0
}

$emoji = Get-LeadingEmoji -Text $firstLine
if ($null -eq $emoji) {
    Write-Host @'
Message de commit invalide : emoji initial manquant.

Attendu : <emoji> <type>(<scope>)?!?: <résumé impératif>
Le hook prepare-commit-msg tire un emoji aléatoire (≠ dernier commit).

Voir docs/commit-convention.md
'@
    exit 1
}

$rest = Remove-LeadingEmoji -Text $firstLine
$types = $script:ConventionalTypes -join '|'
$pattern = "^(?<type>$types)(?<scope>\([a-z0-9][a-z0-9._/-]*\))?(?<breaking>!)?: (?<subject>.+)$"
$match = [regex]::Match($rest, $pattern)
if (-not $match.Success) {
    Write-Host @'
Message de commit invalide.

Attendu : <emoji> <type>(<scope>)?!?: <résumé impératif>
Exemple  : feat(store): add retention purge
(l'emoji est injecté automatiquement)

Voir docs/commit-convention.md
'@
    exit 1
}

$lastEmoji = Get-LastCommitEmoji
if ($null -ne $lastEmoji -and $emoji -eq $lastEmoji) {
    Write-Host "Emoji identique au commit précédent ($lastEmoji) : refuse."
    exit 1
}

$subject = $match.Groups['subject'].Value.TrimEnd()
if ($subject.Length -eq 0) {
    Write-Host 'Sujet vide.'
    exit 1
}
if ($subject.EndsWith('.')) {
    Write-Host 'Pas de point final dans le sujet.'
    exit 1
}
if ($firstLine.Length -gt 72) {
    Write-Host "Ligne de sujet trop longue ($($firstLine.Length) > 72)."
    exit 1
}

# Strip forbidden trailers so the recorded commit never contains them.
$trailerPattern = '^(Signed-off-by|Co-authored-by|Reviewed-by|Acked-by)\s*:'
$cleaned = [System.Collections.Generic.List[string]]::new()
$removed = 0
foreach ($line in [System.IO.File]::ReadAllLines($path, $script:Utf8)) {
    if ($line -match $trailerPattern) {
        $removed++
        continue
    }
    $cleaned.Add($line)
}
if ($removed -gt 0) {
    [System.IO.File]::WriteAllLines($path, $cleaned, $script:Utf8)
}

exit 0

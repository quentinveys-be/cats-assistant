# Shared helpers for commit-msg / prepare-commit-msg hooks.

$script:Utf8 = New-Object System.Text.UTF8Encoding $false

$script:ConventionalTypes = @(
    'feat', 'fix', 'docs', 'refactor', 'test', 'chore', 'perf', 'ci', 'style', 'build'
)

# Unicode ranges used only as a random pool (no type mapping).
$script:EmojiCodepointRanges = @(
    @{ Min = 0x1F300; Max = 0x1F5FF },
    @{ Min = 0x1F600; Max = 0x1F64F },
    @{ Min = 0x1F680; Max = 0x1F6FF },
    @{ Min = 0x1F900; Max = 0x1F9FF },
    @{ Min = 0x1FA70; Max = 0x1FAFF }
)

function Get-LeadingEmoji {
    param([Parameter(Mandatory = $true)][string]$Text)

    if ([string]::IsNullOrEmpty($Text)) { return $null }
    if ($Text[0] -match '[A-Za-z]') { return $null }

    $sb = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $Text.Length) {
        $ch = $Text[$i]
        if ($ch -eq ' ') { break }

        if ([char]::IsHighSurrogate($ch) -and ($i + 1) -lt $Text.Length -and [char]::IsLowSurrogate($Text[$i + 1])) {
            [void]$sb.Append($ch)
            [void]$sb.Append($Text[$i + 1])
            $i += 2
            continue
        }

        $code = [int][char]$ch
        # ZWJ / emoji presentation selector / BMP symbols (e.g. ♻️)
        if ($code -eq 0x200D -or $code -eq 0xFE0F -or ($code -gt 0x7F -and $code -lt 0xD800)) {
            [void]$sb.Append($ch)
            $i++
            continue
        }

        break
    }

    if ($sb.Length -eq 0) { return $null }
    return $sb.ToString()
}

function Remove-LeadingEmoji {
    param([Parameter(Mandatory = $true)][string]$Text)
    $emoji = Get-LeadingEmoji -Text $Text
    if ($null -eq $emoji) { return $Text }
    $rest = $Text.Substring($emoji.Length)
    if ($rest.StartsWith(' ')) { $rest = $rest.Substring(1) }
    return $rest
}

function Get-LastCommitSubject {
    $prevError = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $subject = git log -1 --pretty=format:%s 2>$null
    $exit = $LASTEXITCODE
    $ErrorActionPreference = $prevError
    if ($exit -ne 0 -or [string]::IsNullOrWhiteSpace($subject)) {
        return $null
    }
    return [string]$subject
}

function Get-LastCommitEmoji {
    $subject = Get-LastCommitSubject
    if ($null -eq $subject) { return $null }
    return Get-LeadingEmoji -Text $subject
}

function New-RandomCommitEmoji {
    param([string]$Exclude)

    for ($attempt = 0; $attempt -lt 64; $attempt++) {
        $range = $script:EmojiCodepointRanges | Get-Random
        $codepoint = Get-Random -Minimum $range.Min -Maximum ($range.Max + 1)
        try {
            $emoji = [char]::ConvertFromUtf32($codepoint)
        }
        catch {
            continue
        }

        if ([string]::IsNullOrEmpty($Exclude) -or $emoji -ne $Exclude) {
            return $emoji
        }
    }

    throw "Impossible de tirer un emoji différent du précédent ($Exclude)."
}

function Get-CommitMessagePath {
    param([Parameter(Mandatory = $true)][string]$MessageFile)
    return (Resolve-Path -LiteralPath $MessageFile).Path
}

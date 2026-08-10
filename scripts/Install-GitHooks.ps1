#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

Push-Location $repoRoot
try {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw 'npm introuvable : installe Node.js avant les hooks.'
    }

    npm install
    Write-Host "Husky + lint-staged installés (pre-commit : ESLint, Prettier, Markdownlint)."
    Write-Host "Emoji : prepare-commit-msg / commit-msg (voir docs/commit-convention.md)."
}
finally {
    Pop-Location
}

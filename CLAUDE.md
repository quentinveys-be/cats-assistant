# CATS Assistant

Desktop Windows 11 app (WPF / .NET 8, C#) that passively tracks work activity, correlates it with JIRA tickets, GitLab commits and Outlook meetings, proposes pre-filled SAP CATS timesheet entries, and submits them via the SAP OData API only after explicit user validation.

## Hard rules

- Language: code and identifiers in English; docs and commit messages in French.
- Commit messages: random emoji (≠ previous commit) + `type(scope): résumé` (see docs/commit-convention.md); no trailers; do not pick the emoji (hook). Pre-commit: ESLint + Prettier + Markdownlint (blocking). Install hooks via `scripts/Install-GitHooks.ps1`.
- No admin rights available: user-mode only. No Windows services, no drivers. Startup via HKCU Run key. Self-contained per-user deployment.
- All data stays local. SQLite encrypted; secrets vault: YubiKey HMAC-SHA1 challenge-response (Yubico.YubiKey SDK) + DPAPI. Vault holds JIRA and GitLab tokens ONLY. SAP credentials are NEVER stored (interactive WebView2 logon).
- The Filler must NEVER submit to SAP without an explicit user click (STOP gate).
- Before any SAP submission: cross-check extracted POSID/ZWPID against ValueHelpList (guard rail, see docs/jira-mapping.md).
- No keylogging, no screenshots, no document content capture.
- customfield_10047 (Imputations CATS) contains personal data (name, SAP personnel number): local read only, never re-emitted, never logged.
- Pernr is fetched at runtime (InitialInfos), never hardcoded.
- Never log cookies, CSRF tokens, or vault contents.

## Architecture

- src/CatsAssistant.Collector: Win32 hooks (SetWinEventHook EVENT_SYSTEM_FOREGROUND + EVENT_OBJECT_NAMECHANGE, GetLastInputInfo idle detection, 5 min threshold), tray icon.
- src/CatsAssistant.Store: SQLite (Microsoft.Data.Sqlite) + repositories. Schema: docs/data-model.md.
- src/CatsAssistant.Connectors: Jira (Cloud REST v3 + ADF parser), GitLab (REST + personal token), OutlookCom (COM interop, local Outlook).
- src/CatsAssistant.Correlator: time-block aggregation (>=15 min), JIRA key detection regex `ULISTROIS[-/](\d+)` on window titles and git branches, rule engine.
- src/CatsAssistant.App: WPF UI — daily timeline, review/edit/validate, WebView2 SAP logon window.
- src/CatsAssistant.Filler: SAP OData v2 client. Full protocol spec: docs/sap-cats-api.md. Do not guess payload fields; use the spec.

## Decisions

docs/adr/ (D1-D7, all decided). Read them before structural changes.

## Testing

- xUnit. Correlator rules and the customfield_10045 extraction regex must be fully covered (including the "(hors clients)" trap: extract the LAST parenthesized group).
- No network calls in tests; connectors mocked. SAP client tested against recorded $batch fixtures.

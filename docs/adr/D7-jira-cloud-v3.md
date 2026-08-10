# D7 — Intégration JIRA : API REST Cloud v3 — DÉCIDÉ

Instance ulis-uliege.atlassian.net ; token API personnel (coffre D6). Endpoint recherche : /rest/api/3/search/jql (v2 supprimée par Atlassian). Champs : customfield_10044/10045/10046/10047 — détails et extraction POSID/ZWPID : docs/jira-mapping.md. customfield_10047 contient des données personnelles : lecture locale uniquement, jamais réémis ni loggé. Corrélation : regex ULISTROIS[-/](\d+) sur titres IntelliJ (worktrees) et branches/messages GitLab.

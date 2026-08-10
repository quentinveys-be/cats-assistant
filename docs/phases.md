# Plan de phases (MVP incrémental)

| Phase | Livrable                                                                                                                              | Critère de done                                                                        |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| 1     | Solution .NET + Collector + Store : tray app, hooks foreground/titre, idle, SQLite, purge                                             | 3 jours d'activité capturés et relisibles ; CPU < 1 % en veille                        |
| 2     | Connecteurs : coffre YubiKey, JIRA (v3 + ADF + extraction POSID/ZWPID), GitLab, Outlook COM                                           | Tickets + codes + commits + réunions visibles en base ; regex 10045 couverte par tests |
| 3     | Correlator + UI de revue : timeline journalière, blocs proposés, édition, règles apprises                                             | Une journée reconstituée validable en < 2 min                                          |
| 4     | Filler : logon WebView2, client OData ($batch, CSRF), garde-fou ValueHelpList, STOP gate ; résoudre Q5 (format Note via TimeDataList) | Soumission d'une journée test dans CATS, vérifiée manuellement dans le Fiori           |
| 5     | Durcissement : rétention, export, onboarding config, runbook, Definition of Done                                                      | Runbook complet ; installation propre sur machine vierge sans admin                    |

## État Phase 1

Livrée techniquement le 2026-08-10 : `dotnet build` (solution entière, 7 projets) et `dotnet test` passent tous les deux depuis la racine, 0 avertissement, 0 erreur, 23/23 tests réussis.

Le critère de done complet reste **partiellement vérifié** :

- ✅ Solution .NET + Collector (hooks foreground/titre, idle) + Store (SQLite, purge de rétention)
  - tray app implémentés et testés unitairement (hors code à dépendance Win32/UI directe, non exécutable en environnement headless).
- ⏳ « 3 jours d'activité capturés et relisibles » et « CPU < 1 % en veille » : vérifications manuelles/temporelles en usage réel, **non exécutables automatiquement dans ce chantier** — à valider par l'utilisateur sur une utilisation prolongée de l'app.

Écarts connus par rapport aux ADRs :

- Chiffrement SQLite (ADR D3) non implémenté en Phase 1 — schéma en clair, décision actée à trancher avant un usage réel des données ou avant la Phase 2.

## Points ouverts

| ID  | Question                      | Résolution prévue                                 |
| --- | ----------------------------- | ------------------------------------------------- |
| Q5  | Format exact de LONGTEXT_DATA | Phase 4 : lire une ligne existante (TimeDataList) |
| Q6  | Validation POSID/ZWPID        | Phase 4 : garde-fou ValueHelpList pré-submit      |

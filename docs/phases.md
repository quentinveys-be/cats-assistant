# Plan de phases (MVP incrémental)

| Phase | Livrable                                                                                                                              | Critère de done                                                                        |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| 1     | Solution .NET + Collector + Store : tray app, hooks foreground/titre, idle, SQLite, purge                                             | 3 jours d'activité capturés et relisibles ; CPU < 1 % en veille                        |
| 2     | Connecteurs : coffre YubiKey, JIRA (v3 + ADF + extraction POSID/ZWPID), GitLab, Outlook COM                                           | Tickets + codes + commits + réunions visibles en base ; regex 10045 couverte par tests |
| 3     | Correlator + UI de revue : timeline journalière, blocs proposés, édition, règles apprises                                             | Une journée reconstituée validable en < 2 min                                          |
| 4     | Filler : logon WebView2, client OData ($batch, CSRF), garde-fou ValueHelpList, STOP gate ; résoudre Q5 (format Note via TimeDataList) | Soumission d'une journée test dans CATS, vérifiée manuellement dans le Fiori           |
| 5     | Durcissement : rétention, export, onboarding config, runbook, Definition of Done                                                      | Runbook complet ; installation propre sur machine vierge sans admin                    |

## Points ouverts

| ID  | Question                      | Résolution prévue                                 |
| --- | ----------------------------- | ------------------------------------------------- |
| Q5  | Format exact de LONGTEXT_DATA | Phase 4 : lire une ligne existante (TimeDataList) |
| Q6  | Validation POSID/ZWPID        | Phase 4 : garde-fou ValueHelpList pré-submit      |

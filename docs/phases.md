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
- Purge manuelle (ADR D3, docs/data-model.md) sans déclencheur utilisateur — le service `ActivityEventRetentionPurger` la supporte (seuil injectable), mais seule la purge automatique au démarrage est branchée ; l'entrée d'UI manquante est à couvrir au plus tard en Phase 5 (durcissement rétention).

Audit de clôture (2026-08-11) : relecture complète du code livré contre les ADRs D1–D3 et les règles transverses — stack WPF/.NET 8 conforme (D1) ; capture limitée à SetWinEventHook + GetLastInputInfo, seuil idle 5 min configurable, aucun keylogging ni capture d'écran ni contenu de document, aucun appel réseau (D2) ; user-mode strict (clé Run HKCU uniquement, opt-in décoché par défaut, Collector in-process, pas de service) ; Correlator/Connectors/Filler réduits à des interfaces vides comme exigé. Seuls écarts : les deux points ci-dessus.

## Proposition de plan Phase 2 — Connecteurs

Préalable bloquant, à trancher avant que des données métier (tickets, commits, réunions) ne rejoignent la base :

- **2.0 — Chiffrement SQLite (ADR D3)** : choisir et intégrer la solution de chiffrement — piste principale : SQLCipher via `SQLitePCLRaw.bundle_e_sqlcipher` (changement de provider ADO), clé maître protégée par le coffre de l'étape 2.1. _Action humaine : décision d'architecture._

Étapes (2.1 d'abord — les connecteurs 2.2 et 2.3 consomment ses tokens ; 2.2, 2.3 et 2.4 sont ensuite indépendantes) :

- **2.1 — Coffre de secrets (ADR D6)** : challenge-response HMAC-SHA1 via le SDK Yubico.YubiKey + couche DPAPI per-user ; API stocker/lire/supprimer réservée aux tokens JIRA et GitLab ; YubiKey requise au démarrage (définir le comportement si absente : invite de reconnexion vs mode dégradé sans sync). Jamais de log du contenu du coffre. _Actions humaines : enrôler un slot challenge-response HMAC-SHA1 sur la YubiKey ; présence physique de la clé._
- **2.2 — Connecteur JIRA Cloud v3 (ADR D7, docs/jira-mapping.md)** : client REST `GET /rest/api/3/search/jql` authentifié par le token du coffre ; parseur ADF → texte brut ; extraction POSID/ZWPID depuis `customfield_10045` (dernier groupe parenthésé — le piège « (hors clients) » doit être couvert par les tests, exigence CLAUDE.md) ; persistance dans `jira_tickets`. `customfield_10047` : lecture locale uniquement, jamais réémis ni loggé. Tests sur fixtures JSON enregistrées, aucun appel réseau. _Action humaine : créer le token API Atlassian et le déposer dans le coffre._
- **2.3 — Connecteur GitLab REST** : commits et branches de l'utilisateur via token personnel ; extraction `jira_key` (`ULISTROIS[-/](\d+)`, normalisée `ULISTROIS-<n>`) ; persistance dans `vcs_commits`. Tests mockés. _Action humaine : créer le token GitLab (scope `read_api`) et le déposer dans le coffre._
- **2.4 — Connecteur Outlook COM** : interop avec l'Outlook local ; lecture du calendrier (sujet, organisateur, début/fin — jamais le corps des réunions) ; persistance dans `calendar_events`. Interop isolée derrière `IOutlookConnector` pour rester testable sans Outlook. _Action humaine : Outlook desktop installé et profil configuré._
- **2.5 — Repositories et contraintes** : repositories `jira_tickets`, `vcs_commits`, `calendar_events` ; migration ajoutant les contraintes NOT NULL différées depuis la Phase 1 (reconstruction des tables — dette actée lors de la revue de Phase 1).
- **2.6 — Synchronisation et vérification** : déclenchement des syncs (manuel via le menu tray au minimum, périodique optionnel), gestion d'erreur réseau avec backoff, puis vérification du critère de done de la phase : tickets + codes + commits + réunions visibles en base, regex 10045 couverte par tests.

## État Phase 2

- ✅ **2.1 — Coffre de secrets (ADR D6)** livré : projet `CatsAssistant.Secrets`, `DpapiYubiKeySecretVault` (`ISecretVault` : store/read/delete réservé à `JiraApiToken`/`GitLabPersonalToken`), clé dérivée par challenge-response HMAC-SHA1 YubiKey (HKDF-SHA256) + protection DPAPI `CurrentUser` en couche complémentaire (AES-GCM). `IYubiKeyChallengeResponseClient` isole l'interop matérielle Yubico.YubiKey pour rester testable sans clé physique. Comportement YubiKey absente tranché et documenté dans D6 : mode dégradé sans sync (`YubiKeyNotPresentException`), jamais de blocage de l'app. Aucun log de contenu du coffre. _Reste à faire, hors périmètre 2.1 : enrôlement réel du slot HMAC-SHA1 sur la YubiKey (action humaine), et branchement de l'UI de reconnexion en 2.6._
- ✅ **Câblage coffre → connecteurs 2.2/2.3** : `VaultJiraTokenProvider` et `VaultGitLabTokenProvider` (`CatsAssistant.Connectors`) implémentent `IJiraTokenProvider`/`IGitLabTokenProvider` sur `ISecretVault`, dégradent en `null` sur `YubiKeyNotPresentException`, propagent `SecretVaultException` (coffre corrompu).
- ✅ **2.6 — Synchronisation et vérification** : `CatsAssistant.App.SyncService` orchestre les 3 connecteurs et persiste via les repositories 2.5 (`jira_tickets`, `vcs_commits`, `calendar_events`), avec retry/backoff (`RetryBackoff`, erreurs réseau transitoires uniquement — une erreur de configuration comme un token absent échoue immédiatement), état par connecteur (`Idle`/`Running`/`Success`/`Error`/`Unavailable` + `last_sync`) exposé pour la future UI de pastilles, synchro concurrente ignorée (no-op) pour ne jamais empiler des appels réseau. Déclenchement manuel via le menu tray (« Synchroniser maintenant ») ; périodique optionnel via `CATS_SYNC_INTERVAL_MINUTES`. Câblée dans `App.OnStartup` uniquement si `business.db` est déverrouillée ; chaque connecteur reste `null` (état `Unavailable`, pas de crash) tant qu'il n'est pas configuré. `customfield_10047` n'est jamais lu par le sync (absent du DTO `JiraTicket`). Tests (`SyncServiceTests`) avec connecteurs mockés, aucun appel réseau.
  - Écart assumé : l'instance JIRA est fixée par l'ADR D7, mais l'e-mail du compte (`CATS_JIRA_ACCOUNT_EMAIL`) et la config GitLab (`CATS_GITLAB_BASE_URL`, `CATS_GITLAB_PROJECTS` au format `id:branche,id2:branche2`) n'ont pas encore d'UI d'onboarding (Phase 5) — lues depuis des variables d'environnement en attendant ; absentes, le connecteur correspondant reste désactivé sans bloquer les autres.

## Points ouverts

| ID  | Question                      | Résolution prévue                                 |
| --- | ----------------------------- | ------------------------------------------------- |
| Q5  | Format exact de LONGTEXT_DATA | Phase 4 : lire une ligne existante (TimeDataList) |
| Q6  | Validation POSID/ZWPID        | Phase 4 : garde-fou ValueHelpList pré-submit      |

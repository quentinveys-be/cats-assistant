# Modèle de données (SQLite, v1)

Rétention par défaut : 90 jours (purge automatique + purge manuelle).

Depuis la Phase 2 (décision #10), le schéma v1 ci-dessous est réparti sur **deux fichiers SQLCipher** dans `%LOCALAPPDATA%\CatsAssistant\` :

- `activity.db` — `activity_events`, `settings`. Clé aléatoire protégée par DPAPI (`ProtectedData`, portée `CurrentUser`), stockée à part dans `activity.key`. S'ouvre toujours, sans YubiKey.
- `business.db` — `jira_tickets`, `vcs_commits`, `calendar_events`, `time_blocks`, `rules`. Clé dérivée du challenge-response YubiKey (`step-2.5`).

L'ancienne base en clair `cats-assistant.db` (Phase 1) est migrée one-shot vers `activity.db` par `step-2.2` puis renommée `cats-assistant.db.migrated` (jamais supprimée).

| Table           | Colonnes clés                                                                                                                                                                                                                            |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| activity_events | id, ts, kind (foreground/idle_start/idle_end/title_change), process, window_title, url NULL                                                                                                                                              |
| calendar_events | id, start NOT NULL, end NOT NULL, subject NOT NULL, organizer NULL                                                                                                                                                                       |
| vcs_commits     | id, sha NOT NULL, ts NOT NULL, repo NOT NULL, branch NOT NULL, message NOT NULL, jira_key NULL, UNIQUE(sha, repo)                                                                                                                        |
| jira_tickets    | key PK NOT NULL, summary, status, context, imputation_code_raw, posid, zwpid, effort, last_sync NOT NULL                                                                                                                                 |
| time_blocks     | id, date NOT NULL, start NOT NULL, end NOT NULL, source_summary NOT NULL, jira_key NULL, posid NOT NULL, zwpid NOT NULL, note NOT NULL, duration_hours NOT NULL, status NOT NULL (proposed/edited/validated/submitted), sap_counter NULL |
| rules           | id, matcher_kind NOT NULL (process/title_regex/url_regex/jira_project), matcher_value NOT NULL, target NOT NULL (jira_key ou codes), priority NOT NULL, origin NOT NULL (manual/learned)                                                 |
| settings        | key PK, value (chiffré si sensible)                                                                                                                                                                                                      |

Les 5 tables métier (`jira_tickets`, `vcs_commits`, `calendar_events`, `time_blocks`, `rules`) sont créées côté `business.db` par `BusinessMigrations` (step-2.5/2.6). Les contraintes NOT NULL ci-dessus reprennent exactement la nullabilité C# des DTO produits par les connecteurs (`JiraTicket`, `VcsCommit`, `CalendarEventData`) pour les trois premières ; pour `time_blocks`/`rules` (pas encore de DTO connecteur, Correlator en Phase 3), seules `jira_key` et `sap_counter` sont nullables, conformément à la dette actée en revue de Phase 1 (« seuls `url`, `jira_key`, `sap_counter` sont nullables »). `sha` (vcs_commits) et `context` (jira_tickets, `customfield_10044` désérialisé en texte brut) ne figuraient pas dans le schéma initial `0001_initial_schema.sql` — ajoutés pour correspondre à ce que les connecteurs produisent réellement. `vcs_commits` déduplique sur `(sha, repo)` et non sur `sha` seul : un même sha peut exister dans deux dépôts distincts (fork, miroir, cherry-pick) sans que l'un écrase l'autre.

Notes :

- jira_key normalisée `ULISTROIS-<n>`.
- rules.target : soit une clé JIRA explicite, soit une des valeurs spéciales `LAST_ACTIVE_TICKET` (dernier ticket actif du poste) ou `NO_ATTRIBUTION` (bloc non facturable), résolues par `RuleEvaluator` (`CatsAssistant.Correlator`).
- time_blocks.status suit le cycle : proposed → edited? → validated → submitted.
- sap_counter : Counter renvoyé par SAP à la création (traçabilité).
- Aucune valeur de cookie/token dans cette base ; secrets dans le coffre dédié.

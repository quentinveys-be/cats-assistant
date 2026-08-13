# Modèle de données (SQLite, v1)

Rétention par défaut : 90 jours (purge automatique + purge manuelle).

Depuis la Phase 2 (décision #10), le schéma v1 ci-dessous est réparti sur **deux fichiers SQLCipher** dans `%LOCALAPPDATA%\CatsAssistant\` :

- `activity.db` — `activity_events`, `settings`. Clé aléatoire protégée par DPAPI (`ProtectedData`, portée `CurrentUser`), stockée à part dans `activity.key`. S'ouvre toujours, sans YubiKey.
- `business.db` — `jira_tickets`, `vcs_commits`, `calendar_events`, `time_blocks`, `rules`. Clé dérivée du challenge-response YubiKey (`step-2.5`).

L'ancienne base en clair `cats-assistant.db` (Phase 1) est migrée one-shot vers `activity.db` par `step-2.2` puis renommée `cats-assistant.db.migrated` (jamais supprimée).

| Table           | Colonnes clés                                                                                                                                           |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| activity_events | id, ts, kind (foreground/idle_start/idle_end/title_change), process, window_title, url NULL                                                             |
| calendar_events | id, start NOT NULL, end NOT NULL, subject NOT NULL, organizer NULL                                                                                       |
| vcs_commits     | id, sha NOT NULL UNIQUE, ts NOT NULL, repo NOT NULL, branch NOT NULL, message NOT NULL, jira_key NULL                                                    |
| jira_tickets    | key PK NOT NULL, summary, status, context, imputation_code_raw, posid, zwpid, effort, last_sync NOT NULL                                                |
| time_blocks     | id, date, start, end, source_summary, jira_key NULL, posid, zwpid, note, duration_hours, status (proposed/edited/validated/submitted), sap_counter NULL |
| rules           | id, matcher_kind (process/title_regex/url_regex/jira_project), matcher_value, target (jira_key ou codes), priority, origin (manual/learned)             |
| settings        | key PK, value (chiffré si sensible)                                                                                                                     |

`jira_tickets`, `vcs_commits`, `calendar_events` sont créées côté `business.db` par `BusinessMigrations` (step-2.5) ; les contraintes NOT NULL ci-dessus reprennent exactement la nullabilité C# des DTO produits par les connecteurs (`JiraTicket`, `VcsCommit`, `CalendarEventData`). `time_blocks` et `rules` restent à créer en Phase 3 (Correlator). `sha` (vcs_commits) et `context` (jira_tickets, `customfield_10044` désérialisé en texte brut) ne figuraient pas dans le schéma initial `0001_initial_schema.sql` — ajoutés pour correspondre à ce que les connecteurs produisent réellement.

Notes :

- jira_key normalisée `ULISTROIS-<n>`.
- time_blocks.status suit le cycle : proposed → edited? → validated → submitted.
- sap_counter : Counter renvoyé par SAP à la création (traçabilité).
- Aucune valeur de cookie/token dans cette base ; secrets dans le coffre dédié.

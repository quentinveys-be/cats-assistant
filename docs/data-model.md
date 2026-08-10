# Modèle de données (SQLite, v1)

Rétention par défaut : 90 jours (purge automatique + purge manuelle).

| Table           | Colonnes clés                                                                                                                                           |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| activity_events | id, ts, kind (foreground/idle_start/idle_end/title_change), process, window_title, url NULL                                                             |
| calendar_events | id, start, end, subject, organizer                                                                                                                      |
| vcs_commits     | id, ts, repo, branch, message, jira_key NULL                                                                                                            |
| jira_tickets    | key PK, summary, status, imputation_code_raw, posid, zwpid, effort, last_sync                                                                           |
| time_blocks     | id, date, start, end, source_summary, jira_key NULL, posid, zwpid, note, duration_hours, status (proposed/edited/validated/submitted), sap_counter NULL |
| rules           | id, matcher_kind (process/title_regex/url_regex/jira_project), matcher_value, target (jira_key ou codes), priority, origin (manual/learned)             |
| settings        | key PK, value (chiffré si sensible)                                                                                                                     |

Notes :

- jira_key normalisée `ULISTROIS-<n>`.
- time_blocks.status suit le cycle : proposed → edited? → validated → submitted.
- sap_counter : Counter renvoyé par SAP à la création (traçabilité).
- Aucune valeur de cookie/token dans cette base ; secrets dans le coffre dédié.

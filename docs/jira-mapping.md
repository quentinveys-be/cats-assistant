# Mapping JIRA → CATS

Instance : `https://ulis-uliege.atlassian.net` (Cloud). Auth : token API personnel (coffre YubiKey). API v3 — `/rest/api/2/search` est supprimé côté Atlassian ; utiliser `GET /rest/api/3/search/jql`.

## Champs custom (IDs confirmés)

| id                  | name                           | type           | Usage                                                                                                  |
| ------------------- | ------------------------------ | -------------- | ------------------------------------------------------------------------------------------------------ |
| `customfield_10044` | Imputation                     | textarea (ADF) | Contexte (lien + résumé du ticket)                                                                     |
| `customfield_10045` | Code imputation                | select         | **Source du mapping POSID/ZWPID**                                                                      |
| `customfield_10046` | Effort (somme des imputations) | float          | Info (heures déjà imputées)                                                                            |
| `customfield_10047` | Imputations CATS               | textarea (ADF) | Historique — DONNÉES PERSONNELLES (nom, matricule) : lecture locale uniquement, jamais réémis ni loggé |

Les champs textarea sont au format ADF (`{"type":"doc",...}`) : parseur ADF → texte brut requis.

## Extraction POSID / ZWPID depuis customfield_10045.value

Exemple observé : `"ULIS (hors clients) Dev. Maint. U3 (P.ACSICAT01-01-P-0005 ZS042)"`

- Piège : le libellé contient lui-même des parenthèses ("(hors clients)").
- Règle : extraire le DERNIER groupe parenthésé.
- Regex : `\(([A-Z0-9.\-]+)\s+([A-Z0-9]+)\)\s*$`
  - Groupe 1 → `POSID` (ex. `P.ACSICAT01-01-P-0005`)
  - Groupe 2 → `ZWPID` (ex. `ZS042`)
- Garde-fou obligatoire avant tout submit : vérifier l'existence des codes extraits dans `ValueHelpList` (voir sap-cats-api.md).

## Note CATS (LONGTEXT_DATA)

Format SUPPOSED (déduit du narratif de customfield_10047) : `"<TICKET-KEY> - <résumé>"` (ex. `"ULISTROIS-3428 - …"`). À confirmer en Phase 4 en lisant une ligne existante via TimeDataList (Q5). Tronquer proprement si > ~80 caractères ; positionner `LONGTEXT = "X"`.

## Corrélation activité ↔ ticket

- Clé JIRA : regex `ULISTROIS[-/](\d+)` (normaliser en `ULISTROIS-<n>`).
- Sources : titres de fenêtre IntelliJ (worktrees nommés `ULISTROIS/3101`), branches des commits GitLab, messages de commit.

## Requête type (tickets assignés)

`GET /rest/api/3/search/jql?jql=assignee=currentUser()&fields=summary,status,customfield_10044,customfield_10045,customfield_10046`

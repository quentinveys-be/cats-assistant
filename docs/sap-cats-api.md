# API SAP CATS — spécification (issue du reverse-engineering du 2026-08)

Source : capture réseau de l'app Fiori "Gestion du temps" (session authentifiée).

## Service

- Base : `https://p09.sap.ulg.ac.be:50001/sap/opu/odata/sap/HCM_TIMESHEET_MAN_SRV/`
- OData v2 (SAP Gateway). `DataServiceVersion: 2.0`. `sap-client=010`.
- Accessible uniquement depuis le réseau ULiège/VPN.

## Session (bootstrap WebView2)

- Auth portée par cookies : `SAP_SESSIONID_P09_010`, `sap-usercontext`, `Active`.
- Logon interactif dans une fenêtre WebView2 (gère formulaire/SAML/Kerberos de manière transparente) → extraction des cookies via CoreWebView2CookieManager → injection dans HttpClientHandler.CookieContainer.
- Expiration : sur 401 ou redirection de logon (302), rouvrir la fenêtre WebView2.

## Cycle CSRF

1. `GET {base}?sap-client=010` avec en-tête `x-csrf-token: Fetch`.
2. Réponse 200 : en-tête `x-csrf-token` = jeton.
3. Réinjecter ce jeton dans `x-csrf-token` de toutes les requêtes suivantes, dont le `POST $batch`.

## Endpoints (lecture)

| Endpoint                     | Rôle                                                         |
| ---------------------------- | ------------------------------------------------------------ |
| `$metadata`                  | Contrat complet (EntityTypes ci-dessous)                     |
| `InitialInfos` (`$filter`)   | Infos initiales — source du Pernr au runtime                 |
| `TimeDataList`               | Lignes déjà saisies (modèle clé/valeur FieldName/FieldValue) |
| `WorkCalendars` (`$filter`)  | Calendrier de travail                                        |
| `WorkListCollection`         | Worklist                                                     |
| `ProfileFields`, `Favorites` | Profil / favoris                                             |
| `ValueHelpList`              | Listes déroulantes (voir Value help)                         |

## Écriture d'une ligne

- `POST {base}$batch`, `Content-Type: multipart/mixed; boundary=...`
- Corps : un changeset contenant `POST TimeEntries`.
- Réponse : batch 202 ; opération interne `201 Created` avec l'entité créée.
- En-têtes : `Accept: application/json`, `x-csrf-token`, `X-Requested-With: XMLHttpRequest`, `MaxDataServiceVersion: 2.0`, `DataServiceVersion: 2.0`.

### Entité TimeEntry

| Propriété                                                                                            | Type       | Statut                                      |
| ---------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------- |
| `Pernr`                                                                                              | Edm.String | Requis — récupéré au runtime (InitialInfos) |
| `Counter`                                                                                            | Edm.String | Requis                                      |
| `TimeEntryDataFields`                                                                                | complexe   | Requis                                      |
| `ProfileId`, `Reason`, `Status`, `RefCounter`, `CatsDocNo`, `TimeEntryOperation`, `TimeEntryRelease` | —          | Optionnels                                  |

### TimeEntryDataFields (champs utilisés ; 112 propriétés au total dans le type)

| Propriété       | Type         | Contrainte                  | Rôle UI             |
| --------------- | ------------ | --------------------------- | ------------------- |
| `WORKDATE`      | Edm.DateTime | format `/Date(ms)/`         | Date                |
| `CATSAMOUNT`    | Edm.Decimal  | envoyé en chaîne, ex. `"1"` | Durée (heures)      |
| `POSID`         | Edm.String   | MaxLength=24                | Élément d'OTP (WBS) |
| `ZWPID`         | Edm.String   | MaxLength=8                 | Activité            |
| `LONGTEXT_DATA` | Edm.String   | ~80 car. observés           | Note                |
| `LONGTEXT`      | Edm.String   | MaxLength=1, `"X"` si note  | Indicateur note     |

Note : aucune propriété n'est Nullable=false dans le $metadata — l'obligation Durée/POSID/ZWPID est une règle métier serveur. Ne pas s'appuyer sur le contrat pour la validation ; valider côté client avant envoi.

Le champ Statut UI ("Envoyé pour approbation") = propriété `Status`.

## Value help (F4)

Endpoint générique filtré par champ :

- OTP : `GET ValueHelpList?$filter=FieldName eq 'POSID' and StartDate ... and EndDate ...&$top=&$skip=&sap-client=010`
- Activité : `GET ValueHelpList?$filter=FieldName eq 'ZWPID' and ... and FieldRelated eq ...&...` (ZWPID dépend de POSID via `FieldRelated`)

Entité `ValueHelp` : `Pernr`, `FieldId`, `FieldName`, `FieldValue`, `FieldRelated`, `StartDate`, `EndDate`.

Usage dans l'app : garde-fou pré-submit — vérifier que les POSID/ZWPID extraits de JIRA existent dans ValueHelpList pour la période visée.

## Interdits

- Ne jamais logger cookies ni x-csrf-token.
- Aucune soumission sans clic explicite de validation (STOP gate).

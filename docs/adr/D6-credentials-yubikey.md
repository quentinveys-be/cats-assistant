# D6 — Credentials : coffre YubiKey + DPAPI — DÉCIDÉ

Clé maître dérivée par challenge-response HMAC-SHA1 (SDK Yubico.YubiKey, user-mode, sans admin). Coffre : tokens JIRA + GitLab uniquement. DPAPI per-user en couche complémentaire. YubiKey requise au démarrage. Aucun credential SAP stocké, jamais (voir D4 : logon interactif WebView2). Interdit : logger cookies, jetons CSRF, contenu du coffre.

## Implémentation (step 2.1)

Chaque secret est chiffré individuellement (AES-GCM, clé dérivée par HKDF-SHA256 de la réponse HMAC-SHA1 YubiKey sur un challenge aléatoire propre à ce secret), puis le fichier résultant est protégé DPAPI (`CurrentUser`). Lire un secret exige donc les deux facteurs : session Windows du même utilisateur ET présence physique de la même YubiKey. `CatsAssistant.Secrets.DpapiYubiKeySecretVault` implémente `ISecretVault` (store/read/delete) ; `IYubiKeyChallengeResponseClient` isole l'interop matérielle pour rester testable sans YubiKey physique.

## Comportement si la YubiKey est absente

Décision : **mode dégradé sans sync**, jamais de blocage de l'application entière. `ISecretVault.IsYubiKeyPresent` expose l'état matériel ; `Store`/`TryRead` lèvent `YubiKeyNotPresentException` quand l'opération l'exige. L'appelant (App, sync 2.6) doit intercepter cette exception pour désactiver les fonctionnalités de synchronisation JIRA/GitLab et proposer une invite de reconnexion, sans empêcher la consultation/édition des données déjà en base (timeline, blocs proposés). Raison : l'app est un tracker passif en tâche de fond — l'absence ponctuelle de la clé physique (oubliée, débranchée) ne doit pas interrompre le suivi d'activité déjà collecté.

## Implémentation (step 2.5 — clé maître de `business.db`)

`CatsAssistant.Secrets.BusinessMasterKeyProvider` dérive la clé maître de `business.db` par le même mécanisme HKDF-SHA256 sur réponse HMAC-SHA1 YubiKey, dérivée une seule fois au démarrage et gardée en mémoire pour tout le process (le slot est enrôlé `--touch` : chaque dérivation exige un appui physique, jamais répété à chaque synchronisation). Deux points assumés :

- Le challenge est persisté en clair (`business.challenge`, non sensible seul) pour que la clé dérivée reste stable entre démarrages. **Sa perte ou sa régénération rend `business.db` définitivement illisible** (nouvelle clé dérivée ≠ clé de chiffrement d'origine) : `App.OpenBusinessDatabase` intercepte alors `SqliteException`/`IOException` et bascule en mode dégradé plutôt que de crasher, mais les données métier existantes restent perdues sans sauvegarde du challenge.
- La clé dérivée est gardée en `string` Base64 immuable (non zéroïsable) pour la durée du process, contrairement aux buffers `byte[]` zéroïsés ailleurs dans `CatsAssistant.Secrets`. Compromis assumé : une dérivation unique par démarrage, et la chaîne de connexion SQLCipher conserve de toute façon la clé en mémoire tant que la base est ouverte.

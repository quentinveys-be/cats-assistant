# D6 — Credentials : coffre YubiKey + DPAPI — DÉCIDÉ

Clé maître dérivée par challenge-response HMAC-SHA1 (SDK Yubico.YubiKey, user-mode, sans admin). Coffre : tokens JIRA + GitLab uniquement. DPAPI per-user en couche complémentaire. YubiKey requise au démarrage. Aucun credential SAP stocké, jamais (voir D4 : logon interactif WebView2). Interdit : logger cookies, jetons CSRF, contenu du coffre.

## Implémentation (step 2.1)

Chaque secret est chiffré individuellement (AES-GCM, clé dérivée par HKDF-SHA256 de la réponse HMAC-SHA1 YubiKey sur un challenge aléatoire propre à ce secret), puis le fichier résultant est protégé DPAPI (`CurrentUser`). Lire un secret exige donc les deux facteurs : session Windows du même utilisateur ET présence physique de la même YubiKey. `CatsAssistant.Secrets.DpapiYubiKeySecretVault` implémente `ISecretVault` (store/read/delete) ; `IYubiKeyChallengeResponseClient` isole l'interop matérielle pour rester testable sans YubiKey physique.

## Comportement si la YubiKey est absente

Décision : **mode dégradé sans sync**, jamais de blocage de l'application entière. `ISecretVault.IsYubiKeyPresent` expose l'état matériel ; `Store`/`TryRead` lèvent `YubiKeyNotPresentException` quand l'opération l'exige. L'appelant (App, sync 2.6) doit intercepter cette exception pour désactiver les fonctionnalités de synchronisation JIRA/GitLab et proposer une invite de reconnexion, sans empêcher la consultation/édition des données déjà en base (timeline, blocs proposés). Raison : l'app est un tracker passif en tâche de fond — l'absence ponctuelle de la clé physique (oubliée, débranchée) ne doit pas interrompre le suivi d'activité déjà collecté.

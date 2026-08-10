# D6 — Credentials : coffre YubiKey + DPAPI — DÉCIDÉ

Clé maître dérivée par challenge-response HMAC-SHA1 (SDK Yubico.YubiKey, user-mode, sans admin). Coffre : tokens JIRA + GitLab uniquement. DPAPI per-user en couche complémentaire. YubiKey requise au démarrage. Aucun credential SAP stocké, jamais (voir D4 : logon interactif WebView2). Interdit : logger cookies, jetons CSRF, contenu du coffre.

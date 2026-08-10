# D5 — Classification : règles + heuristiques (v1), LLM (v2) — DÉCIDÉ

v1 : détection clé JIRA (regex) + moteur de règles déclaratives ; chaque correction manuelle devient une règle candidate (apprentissage supervisé léger). Le mapping codes SAP est porté par JIRA (customfield_10045) : la classification se réduit à l'attribution activité → ticket. Le cœur du Correlator est la reconstruction des durées (blocs >= 15 min, fusion, arrondis). v2 : LLM local (Ollama) pour les blocs non classés, opt-in.

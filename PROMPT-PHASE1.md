# Prompt d'amorçage — à coller dans Claude Code (répertoire du repo)

---

Lis CLAUDE.md, docs/phases.md et les ADRs (docs/adr/). Objectif de cette session : Phase 1 uniquement.

Tâches, dans l'ordre, avec un commit par étape cohérente :

1. Initialise la solution .NET 8 : CatsAssistant.sln avec les projets App (WPF), Collector, Store, Correlator, Connectors, Filler (interfaces vides pour Correlator/Connectors/Filler — ne PAS les implémenter en Phase 1), et tests/CatsAssistant.Tests (xUnit). .gitignore et .editorconfig adaptés.
2. Store : schéma SQLite conforme à docs/data-model.md, migrations simples (table schema_version), repositories pour activity_events, purge de rétention (90 j, configurable).
3. Collector : SetWinEventHook (EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_NAMECHANGE) via P/Invoke, GetLastInputInfo (idle 5 min), écriture des événements en base, résilience (rechargement du hook si perdu).
4. App : icône tray (démarrage/pause de la capture, ouverture du dossier données, quitter), enregistrement au démarrage via clé Run HKCU (opt-in), fenêtre minimale affichant les événements du jour (liste brute, pas de timeline — la timeline est en Phase 3).
5. Tests : agrégation des événements bruts (fusion des doublons de titre, bornage par idle), et tests du repository.

Contraintes : user-mode strict (aucun droit admin), CPU < 1 % en veille, aucune donnée hors machine. Vérifie le build à chaque étape (dotnet build, dotnet test). En fin de session : mets à jour docs/phases.md (état Phase 1) et propose le plan de la Phase 2.

---

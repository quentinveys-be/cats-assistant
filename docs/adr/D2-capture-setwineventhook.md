# D2 — Capture d'activité : SetWinEventHook — DÉCIDÉ

EVENT_SYSTEM_FOREGROUND + EVENT_OBJECT_NAMECHANGE (changements de titre), GetLastInputInfo pour l'inactivité (seuil 5 min, configurable). Event-driven : précis, faible coût CPU. User-mode, aucun droit admin requis. Rejeté : polling GetForegroundWindow (imprécis, coûteux). Exclusions : keylogging, capture d'écran, contenu de documents.

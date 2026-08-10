# CODING AGENTS: READ THIS FIRST

Handoff bundle Claude Design → implémentation WPF (.NET 8). Prototypes HTML/CSS/JS, pas du code prod.

## Ordre de lecture

1. **`screens/cats-assistant.dc.html`** — écran principal (lire en entier, ne pas survoler).
2. Suivre ses liens / imports : `../_runtime/support.js`, puis les planches et tokens liés.
3. En cas d’ambiguïté : demander confirmation avant d’implémenter.

## Carte

| Chemin | Rôle |
| --- | --- |
| `screens/cats-assistant.dc.html` | App interactive (timeline, review, validation) |
| `screens/planches-clair-sombre.dc.html` | Comparaison thème clair / sombre |
| `screens/planche-etats.dc.html` | États annexes (vide, erreur, idle, etc.) |
| `tokens/design-tokens.dc.html` | Palette + clés XAML |
| `_runtime/support.js` | Runtime Claude Design (partagé) |
| `design-system/industry/` | DS Industry exporté (référence) |

## Règles d’implémentation

- Recréer le rendu pixel-perfect dans la stack cible (WPF) ; ne pas copier la structure interne des prototypes sauf si elle colle.
- Ne pas ouvrir ces fichiers dans un navigateur ni prendre de screenshots sauf demande explicite — dimensions, couleurs et layout sont dans le source.
- Les planches portent leurs jetons **inline** ; `design-system/industry/` n’est **pas** importé par les `.dc.html` (référence visuelle / tokens Industry seulement).

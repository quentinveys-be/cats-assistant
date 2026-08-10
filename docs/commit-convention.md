# Convention de messages de commit

Format (une ligne, **sans trailer**) :

```text
<emoji> <type>(<scope>)?: <résumé impératif>
```

L’emoji est **aléatoire** : ne pas le choisir. Le hook `prepare-commit-msg` en tire un au hasard, **obligatoirement différent** de celui du commit précédent. Aucune association emoji ↔ type.

Écrire uniquement la partie Conventional Commits, par ex. :

```text
feat(store): add retention purge
docs(adr): add D4 sap odata decision
chore(git): install commit-msg hook
```

## Types

`feat` · `fix` · `docs` · `refactor` · `test` · `chore` · `perf` · `ci` · `style` · `build`

## Règles

- Sujet en **impératif**, ≤72 caractères pour la ligne entière (emoji inclus), **pas de point final**.
- `scope` optionnel, kebab-case (`store`, `sap`, `jira`).
- Corps optionnel ; **pas de trailer** (`Signed-off-by`, `Co-authored-by`, etc.).
- Breaking change : `!` après le type/scope (`feat(api)!: rename TimeEntry fields`).
- `commit-msg` refuse un emoji identique à celui de `HEAD`.

## Application locale

```powershell
.\scripts\Install-GitHooks.ps1
```

Installe Husky (`core.hooksPath` géré par Husky) :

- `pre-commit` : ESLint + Prettier + Markdownlint via `lint-staged` (**bloquant** si erreurs restantes)
- `prepare-commit-msg` / `commit-msg` : emoji aléatoire + validation Conventional Commits

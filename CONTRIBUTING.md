# Contributing

Use English for source comments, documentation, issues, and player-facing text.
Keep changes focused on one component where possible.

1. Read the component's README and `AGENTS.md` if present.
2. Build the affected component using [the root guide](README.md).
3. Run its existing tests and `python scripts/validate_repository.py`.
4. Explain the behavior change, validation, and remaining limitations in a pull request.

Use synthetic fixtures. Never submit real accounts, connection passwords,
private keys, client files, game data dumps, or runtime captures. Configuration
examples must contain blank values or explicit placeholders.

Do not change protocol constants, schemas, serialized names, or compatibility
profiles without a coordinated migration and compatibility evidence. Keep
upstream copyright notices and third-party license terms.

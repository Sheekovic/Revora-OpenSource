# Revora monorepo

This repository combines `launcher/`, `website/`, and `server/`.

- The primary frontend is `website/site/`: plain HTML, CSS and JavaScript.
  Preserve its buildless/static deployment and English/Arabic accessibility.
  Do not reintroduce ASP.NET or a frontend framework as a website prerequisite.
- ASP.NET under `website/src/` is an optional preserved backend/reference.
- GitHub Pages receives only `website/site/`; `.local/` is private operator data.

- Follow the instructions within each component for changes under it.
- Resolve paths relative to the component's own root. Do not move its build
  props or targets to the monorepo root.
- Preserve protocol, database, signing, authorization, and client hash gates.
- The server targets 6711; imported launcher profiles also contain 7630 work.
  Do not describe them as compatible without a matching signed profile and a
  real end-to-end test.
- Keep secrets, accounts, production data, client distributions, packet captures,
  dumps, private research, and local migration mirrors outside tracked content.
- Do not import missing legacy binaries without provenance and distribution
  rights. Do not weaken build or runtime checks to hide missing dependencies.
- Run the relevant component build/tests and `python scripts/validate_repository.py`.
- Keep third-party notices and license exceptions intact.
- Production deployment, original repository deletion, and original history
  rewriting are separate operations requiring an explicit owner request.

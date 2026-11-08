# Website

The original `Revora.Web` repository is preserved under `website/`, including
its shared libraries, SQL migrations, deployment examples, tests, tools, and
historical protocol projects. The public HTTP application is
`website/src/Revora.Web/Revora.Web.csproj`.

## Build and test

Install the .NET 10 SDK, then run from the monorepo root:

```powershell
./scripts/Build.ps1 -Component Website
./scripts/Test.ps1 -Component Website
```

Building `Revora.WebTests` also builds the web application and its project
dependencies. This test program uses synthetic checks rather than a production
database. Other integration and PostgreSQL test programs have separate runtime
requirements and are not implied by this check.

## Operation

Follow the configuration and deployment documents in [`website/docs/`](../website/docs/)
and the examples in [`website/deploy/`](../website/deploy/). Run deployment tools
from `website/` when their instructions use component-relative paths.

Keep development on loopback. Production database provisioning, HTTPS,
registration settings, backups, and rollout are separate operator tasks.
Existing documentation includes earlier milestones; its dated client/runtime
claims do not establish compatibility with the current 6711 server.

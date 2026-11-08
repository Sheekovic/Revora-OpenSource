# Web Platform Architecture

## Boundaries and flow

`Revora.Web` is an ASP.NET Core server-rendered edge application behind nginx. nginx owns public ports 80/443; the application binds only to an explicitly configured loopback endpoint. Public page rendering, localization and HTTP concerns live in Web. Identity, content and distribution workflows depend on Application interfaces. Domain invariants remain protocol- and transport-neutral. Persistence adapters implement repositories without leaking SQL into routes.

```text
Browser -> nginx TLS -> 127.0.0.1:5080 Revora.Web
                              |
                              v
                       Application ports
                         /           \
                    Domain       Persistence adapters
                                      |
                              local PostgreSQL (future)
```

Website sessions do not become patch-5165 sessions. A future reviewed compatibility adapter may authorize a narrowly scoped handoff; the existing 256-bit internal token remains an internal abstraction until its client-wire representation is verified.

## Runtime model

- Server-rendered localized routes under `/ar/` and `/en/`; `/` is a neutral, crawlable language choice.
- Essential navigation, content, forms, validation and errors work without JavaScript.
- Progressive enhancement is limited to presentation and never carries secrets.
- Explicit liveness and readiness endpoints distinguish process health from dependency readiness.
- SIGTERM/SIGINT use ASP.NET Core graceful shutdown. Structured logs redact identity/security material.
- Production refuses to start when its required protected configuration is missing or when configured to bind publicly.

## Deployment model

A self-contained `linux-x64` artifact is staged, hash-verified and launched as `amp` through `LOCALRevoraWeb-main`. AMP performs no source build, shell wrapper, Git operation, container elevation or package restore. Updates remain disabled until signed release artifacts and rollback are operational.

The static Coming Soon site is the atomic rollback target. nginx changes are narrowly backed up, syntax-tested and gracefully reloaded only after two application lifecycle cycles and route/security checks pass.

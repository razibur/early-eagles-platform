# Early Eagles Platform

Multi-tenant B2B SaaS platform for badminton clubs, owned and developed by **Skylights LLC**. The Early Eagles club is Tenant 0 — the first real customer used to validate every feature before it reaches paying tenants.

## Status

**Phase 0 — Enterprise Foundations, in progress (~24%).** The web app (Next.js 16) and the
backend (ASP.NET Core 10, Clean Architecture) are scaffolded, and the **multi-tenancy core** is
built: tenant context, subdomain resolution middleware, the `ITenantScoped` marker, EF Core with
a global tenant query filter, the initial migration, and a Testcontainers-based test gate. See
[`PROGRESS.md`](./PROGRESS.md) for the live task-by-task status.

## Documents

Read these in order to get the full picture:

| # | Document | What it covers |
|---|---|---|
| 1 | [`project-scope.md`](./project-scope.md) | Problem · Solution · Features (per phase) |
| 2 | [`docs/adr/0002-enterprise-saas-reframe.md`](./docs/adr/0002-enterprise-saas-reframe.md) | **Operative architecture decision** — stack picks and rationale |
| 3 | [`Plan.md`](./Plan.md) | Architecture · stack · 9-phase roadmap · cost model |
| 4 | [`Implementation-Plan.md`](./Implementation-Plan.md) | ~250 numbered tasks across 9 phases |
| 5 | [`PROGRESS.md`](./PROGRESS.md) | Live status dashboard — what's done, in-flight, next |
| 6 | [`docs/developer-walkthrough.md`](./docs/developer-walkthrough.md) | **Build-from-zero guide** — recreate the project by hand, step by step |
| 7 | [`CLAUDE.md`](./CLAUDE.md) | Guidance for Claude Code (AI assistant) |

Historical context and rejected alternatives live under [`docs/`](./docs/) — read only if you need to understand why a particular path was not chosen.

## Contact

For licensing, partnership, or commercial inquiries: **moh.razib@gmail.com**

## License

**Proprietary.** Copyright © 2026 Skylights LLC. All rights reserved. See [`LICENSE`](./LICENSE).

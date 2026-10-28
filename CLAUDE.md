# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status — Phase 0 in progress (web app scaffolded)

Phase 0 has started. The web app is scaffolded under [`apps/web`](./apps/web) (Next.js 16.4 + React 19.2 + Tailwind v4 + TypeScript) with shadcn/ui (Base UI variant) and Recharts wired up. A sample dashboard with three charts (bar / line / area using fake club data) is on `/`.

**Not yet started:** `infra/` (Bicep), Clerk auth, next-intl i18n, the PWA manifest + push (the service worker is in, per ADR-0011), Sentry, GitHub Actions, the tenant-isolation test gate (P0-034). The backend solution is scaffolded under [`apps/api`](./apps/api) on **ASP.NET Core 10** ([ADR-0006](./docs/adr/0006-target-dotnet-10-lts.md) supersedes the .NET 9 pick from ADR-0002) but has no domain entities, no DbContext, and no Clerk integration yet — only Clean Architecture project skeletons plus a NetArchTest dependency-direction gate. Treat the chart work as one Phase 0 task ("scaffold web + pick chart library"), not a sign that the full phase is done.

### Package manager

**npm 11** with workspaces, defined at the repo root. pnpm was the preferred pick but `corepack enable` requires admin rights on Windows (Node is in `Program Files`). To switch to pnpm later: run `corepack enable pnpm` from an elevated terminal, add `"packageManager": "pnpm@<version>"` to root `package.json`, and convert the workspace scripts.

### Commands

Run from the repo root:

```pwsh
npm install                    # install all workspace deps
npm run dev                    # start Next dev server (apps/web on :3000)
npm run build                  # production build for apps/web
npm run lint                   # eslint via next lint
```

Direct workspace targeting also works: `npm --workspace web run <script>`.

### Important Next.js 16 caveat

[`apps/web/AGENTS.md`](./apps/web/AGENTS.md) warns that Next.js 16 has breaking changes from older versions. Before writing or modifying Next-specific code (routing, caching, server components, middleware, etc.), consult Context7 (`/vercel/next.js`) or `apps/web/node_modules/next/dist/docs/` for current syntax. Default to fresh docs, not training-data muscle memory.

### Charting

shadcn/ui Charts (Recharts under the hood) is the chosen library. Chart primitives live in [`apps/web/components/ui/chart.tsx`](./apps/web/components/ui/chart.tsx); sample feature charts are in [`apps/web/components/charts/`](./apps/web/components/charts/). Chart colors use the CSS variables `--chart-1` through `--chart-5` defined in [`apps/web/app/globals.css`](./apps/web/app/globals.css). New chart components must be Client Components (Recharts requires it) and should be composed into Server Component pages.

## Where to start (read these in order)

1. **[`PROGRESS.md`](./PROGRESS.md)** — live status dashboard. Scan in 30 seconds to see what's done, in-flight, deferred, and next. Each task carries a one-line `verify:` step. **Update this every time you finish or block on a task.**
2. **[`project-scope.md`](./project-scope.md)** — what the platform is (B2B multi-tenant SaaS for badminton clubs) and what features each phase delivers.
3. **[`docs/adr/0002-enterprise-saas-reframe.md`](./docs/adr/0002-enterprise-saas-reframe.md)** — **the operative architecture decision.** Defines stack picks and why. Supersedes most of ADR-0001. Subsequent ADRs ([0003](./docs/adr/0003-defer-i18n.md) defer i18n · [0004](./docs/adr/0004-role-based-landing-and-routing.md) role-based landing · [0005](./docs/adr/0005-light-social-layer.md) light social layer · [0006](./docs/adr/0006-target-dotnet-10-lts.md) .NET 10 LTS instead of .NET 9 · [0007](./docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md) martinothamar/Mediator + Mapperly instead of MediatR + AutoMapper · [0008](./docs/adr/0008-azure-managed-redis.md) Azure Managed Redis instead of the retired Azure Cache for Redis · [0009](./docs/adr/0009-local-dev-no-cloud-dev-environment.md) dev runs locally, no standing cloud dev · [0010](./docs/adr/0010-ghcr-instead-of-acr.md) images on GitHub Container Registry, not ACR · [0011](./docs/adr/0011-hand-written-service-worker-instead-of-next-pwa.md) hand-written service worker instead of `next-pwa` · [0012](./docs/adr/0012-gitleaks-and-semgrep-instead-of-github-advanced-security.md) gitleaks + Semgrep CE in CI instead of paid GitHub secret scanning / CodeQL) refine specific decisions on top of it.
4. **[`Plan.md`](./Plan.md)** — current architecture + stack table + 9-phase roadmap + cost model.
5. **[`Implementation-Plan.md`](./Implementation-Plan.md)** — ~250 numbered tasks across 9 phases, with dependencies and acceptance criteria. Phases 0–2 are detailed; 3–9 are sketched and will be re-planned.

ADR-0001, the pre-BrainStorm Supabase alternative, and the chat log under `docs/` are historical context — read them only if you need to understand why a particular path was rejected.

## Project framing (load-bearing context)

- **Enterprise B2B SaaS**, intended to be sold to other clubs. Quality and sellability over speed. No fixed deadline.
- **Tenant 0** is the Early Eagles club (~35 middle-aged Bangladeshi men, weekly Sunday play, $15 Zelle dues). They are the first real user; not a hypothetical.
- **Multi-tenant from day one** — pool model, single Postgres, `tenant_id` on every domain table, EF Core global query filters, tenant-resolution middleware, automated isolation tests in CI.
- **Language: English only for now.** Bangla / `next-intl` / `IStringLocalizer` are **deferred** per [ADR-0003](./docs/adr/0003-defer-i18n.md) until a Bangla-first tenant is near production or the platform is being marketed to non-English clubs.
- **Member dues** (Zelle, e-Transfer, cash) are *tracked, not collected*. Stripe is used **only** for tenant subscription billing.

## Operative stack (per ADR-0002)

| Layer | Pick |
|---|---|
| Backend | ASP.NET Core 10 + Clean Architecture (Domain / Application / Infrastructure / API) + EF Core 10 (per [ADR-0006](./docs/adr/0006-target-dotnet-10-lts.md), superseding the .NET 9 pick in ADR-0002) |
| Database | Azure Database for PostgreSQL — Flexible Server |
| Realtime | SignalR + Azure SignalR Service |
| Auth | Clerk B2B Organizations (Clerk Org = Tenant) |
| Object storage | Cloudflare R2 |
| Frontend | Next.js 16 (App Router) + TypeScript + Tailwind + shadcn/ui + hand-written service worker (no PWA plugin, per [ADR-0011](./docs/adr/0011-hand-written-service-worker-instead-of-next-pwa.md)) + next-intl |
| Cache + locks | Azure Managed Redis (per [ADR-0008](./docs/adr/0008-azure-managed-redis.md), superseding Azure Cache for Redis in ADR-0002) |
| Message bus | Azure Service Bus |
| Subscription billing | Stripe Billing |
| Cloud | Azure |
| IaC | Bicep |
| CI/CD | GitHub Actions — dev runs locally, staging on demand, manual prod promote (per [ADR-0009](./docs/adr/0009-local-dev-no-cloud-dev-environment.md)) |
| Observability | App Insights + OpenTelemetry + Sentry + Better Stack + Grafana |

When working on a task, prefer the picks above. If a different choice seems better, write a new ADR (do not edit existing ones).

## Conventions

### ADRs are immutable history
ADRs live under [`docs/adr/`](./docs/adr/). When a decision changes, write a **new** ADR (`0003-...`, `0004-...`) that supersedes the old one. Never edit an accepted ADR. Mark superseded ADRs in their status line.

### Task IDs from the Implementation Plan
Tasks are referenced by stable IDs like `P0-034`, `P1-014`. Bundled tasks are decomposed with dotted sub-IDs (`P0-020.1`, `P0-020.2`, …) so each piece is independently verifiable. When implementing a task, reference its ID in commits and PR titles:
```
P0-034.3: tenant isolation test for cross-tenant query
```
If a task gets split or estimates change significantly, append to `Implementation-Plan.md` rather than rewriting.

### Tasks must be small features
Every task — new or refined — is 1–3 hours of work, single concept, independently verifiable, committable alone. Reject "build X system" framing on sight and decompose into the 6–10 sub-tasks it really is. Each task carries a one-line `verify:` step in PROGRESS.md.

### Multi-tenancy is non-negotiable
Every new domain entity has `TenantId`. Every query is filtered by EF Core global query filter. Every command/query handler runs under `ITenantContext`. **Phase 0 task `P0-034` (tenant isolation integration test) is the gate for adding any feature** — it must stay green.

### Audit logging is non-negotiable
Every state-changing operation produces an append-only `AuditEvent` row, scoped to its tenant. The `AuditBehavior` Mediator pipeline behavior handles this — don't bypass it. (Mediator = `martinothamar/Mediator` per [ADR-0007](./docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md), not MediatR.)

### i18n is deferred (per ADR-0003)
English only for now. Do **not** wire `next-intl`, `IStringLocalizer`, locale routing, Bangla font, or per-string translation passes. Mitigation for the eventual retrofit: keep user-visible strings out of business logic so a future extraction pass stays mechanical.

### Feature flags before features
Every new user-facing capability ships behind a Flagsmith flag, off by default in prod. Tenants opt in.

## Out of scope (do not add unless asked)

- Online member-dues collection (Stripe / bKash / etc.). Stripe is only for tenant subscription billing.
- Generic tournament-bracket engine — round-robin / single-elim only when Phase 4 lands.
- Court / equipment booking conflict resolution.
- Replacing WhatsApp casual chat in Phase 1 — Phase 3 introduces in-app chat.
- Microsoft 365 Teams Agent and Unity XR are deliberately **deferred to Phase 8 / Phase 9**, not Phase 0.

## Repo layout (Phase 0 target, per `Implementation-Plan.md` P0-019)

```
apps/
  api/                  ASP.NET Core 10 (Clean Architecture)  — solution + 7 projects scaffolded
  web/                  Next.js 16                            — scaffolded
infra/                  Bicep modules + per-environment main.bicep — not yet created
docs/                   ADRs, alternatives, chat-log, runbooks
.github/workflows/      CI — backend, web, security, api-image (ghcr.io), sbom
```

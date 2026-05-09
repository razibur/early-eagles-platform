# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status — documentation-only

There is **no code in this repository yet**. The repo currently contains only planning and architecture documents. Phase 0 (the first implementation phase) has not started. There is no `package.json`, no `.sln`, no Bicep, no Dockerfile — nothing to build, run, lint, or test until Phase 0 begins.

When code does land, this file must be updated with:
- Build / run / test commands (likely `dotnet build`, `dotnet test`, `pnpm dev`, `bicep build`, etc.)
- A more concrete description of the running architecture
- Anything else future-Claude needs to be productive

## Where to start (read these in order)

1. **[`project-scope.md`](./project-scope.md)** — what the platform is (B2B multi-tenant SaaS for badminton clubs) and what features each phase delivers.
2. **[`docs/adr/0002-enterprise-saas-reframe.md`](./docs/adr/0002-enterprise-saas-reframe.md)** — **the operative architecture decision.** Defines stack picks and why. Supersedes most of ADR-0001.
3. **[`Plan.md`](./Plan.md)** — current architecture + stack table + 9-phase roadmap + cost model.
4. **[`Implementation-Plan.md`](./Implementation-Plan.md)** — ~250 numbered tasks across 9 phases, with dependencies and acceptance criteria. Phases 0–2 are detailed; 3–9 are sketched and will be re-planned.

ADR-0001, the pre-BrainStorm Supabase alternative, and the chat log under `docs/` are historical context — read them only if you need to understand why a particular path was rejected.

## Project framing (load-bearing context)

- **Enterprise B2B SaaS**, intended to be sold to other clubs. Quality and sellability over speed. No fixed deadline.
- **Tenant 0** is the Early Eagles club (~35 middle-aged Bangladeshi men, weekly Sunday play, $15 Zelle dues). They are the first real user; not a hypothetical.
- **Multi-tenant from day one** — pool model, single Postgres, `tenant_id` on every domain table, EF Core global query filters, tenant-resolution middleware, automated isolation tests in CI.
- **Languages: English + Bangla** from day one at every layer (backend errors, emails, push, UI).
- **Member dues** (Zelle, e-Transfer, cash) are *tracked, not collected*. Stripe is used **only** for tenant subscription billing.

## Operative stack (per ADR-0002)

| Layer | Pick |
|---|---|
| Backend | ASP.NET Core 9 + Clean Architecture (Domain / Application / Infrastructure / API) + EF Core 9 |
| Database | Azure Database for PostgreSQL — Flexible Server |
| Realtime | SignalR + Azure SignalR Service |
| Auth | Clerk B2B Organizations (Clerk Org = Tenant) |
| Object storage | Cloudflare R2 |
| Frontend | Next.js 16 (App Router) + TypeScript + Tailwind + shadcn/ui + next-pwa + next-intl |
| Cache + locks | Azure Cache for Redis |
| Message bus | Azure Service Bus |
| Subscription billing | Stripe Billing |
| Cloud | Azure |
| IaC | Bicep |
| CI/CD | GitHub Actions (dev → staging → prod, manual prod promote) |
| Observability | App Insights + OpenTelemetry + Sentry + Better Stack + Grafana |

When working on a task, prefer the picks above. If a different choice seems better, write a new ADR (do not edit existing ones).

## Conventions

### ADRs are immutable history
ADRs live under [`docs/adr/`](./docs/adr/). When a decision changes, write a **new** ADR (`0003-...`, `0004-...`) that supersedes the old one. Never edit an accepted ADR. Mark superseded ADRs in their status line.

### Task IDs from the Implementation Plan
Tasks are referenced by stable IDs like `P0-034`, `P1-014`. When implementing a task, reference its ID in commits and PR titles:
```
P0-034: tenant isolation integration test
```
If a task gets split or estimates change significantly, append to `Implementation-Plan.md` rather than rewriting.

### Multi-tenancy is non-negotiable
Every new domain entity has `TenantId`. Every query is filtered by EF Core global query filter. Every command/query handler runs under `ITenantContext`. **Phase 0 task `P0-034` (tenant isolation integration test) is the gate for adding any feature** — it must stay green.

### Audit logging is non-negotiable
Every state-changing operation produces an append-only `AuditEvent` row, scoped to its tenant. The `AuditBehavior` MediatR pipeline behavior handles this — don't bypass it.

### i18n at every layer
Add Bangla strings alongside English from the first commit. Do not "add translations later" — the architecture (`next-intl` + `IStringLocalizer`) makes both equally easy.

### Feature flags before features
Every new user-facing capability ships behind a Flagsmith flag, off by default in prod. Tenants opt in.

## Out of scope (do not add unless asked)

- Online member-dues collection (Stripe / bKash / etc.). Stripe is only for tenant subscription billing.
- Generic tournament-bracket engine — round-robin / single-elim only when Phase 4 lands.
- Court / equipment booking conflict resolution.
- Replacing WhatsApp casual chat in Phase 1 — Phase 3 introduces in-app chat.
- Microsoft 365 Teams Agent and Unity XR are deliberately **deferred to Phase 8 / Phase 9**, not Phase 0.

## When code starts (Phase 0)

The expected layout (per `Implementation-Plan.md` task P0-019) is a monorepo:

```
apps/
  api/                  ASP.NET Core 9 (Clean Architecture)
  web/                  Next.js 16
infra/                  Bicep modules + per-environment main.bicep
docs/                   ADRs, alternatives, chat-log, runbooks
.github/workflows/      CI/CD
```

When the first code lands, update this CLAUDE.md with the actual package manager (`pnpm` vs `npm`), the actual `dotnet` solution path, and any per-environment commands. Until then, there is nothing to run.

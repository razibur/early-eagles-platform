# ADR-0006: Target .NET 10 (LTS) instead of .NET 9 (STS)

- **Status:** Accepted (supersedes the runtime-version line of [ADR-0002](./0002-enterprise-saas-reframe.md) — "ASP.NET Core 9 + Clean Architecture + EF Core 9 (backend)" — and the matching cells in [ADR-0001](./0001-stack-and-architecture.md))
- **Date:** 2026-06-18
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`CLAUDE.md`](../../CLAUDE.md), [`Plan.md`](../../Plan.md), [`Implementation-Plan.md`](../../Implementation-Plan.md), [`PROGRESS.md`](../../PROGRESS.md)

## Context

ADR-0001 and ADR-0002 both fixed the backend runtime at **ASP.NET Core 9 + EF Core 9**. Those ADRs were written before .NET 10 shipped.

State of the .NET release train at the time of this ADR (June 2026):

- **.NET 9 is an STS release.** Support window: November 2024 → **May 2026**. It is **out of mainstream support as of last month.**
- **.NET 10 is the current LTS release.** Shipped November 2025. Supported through November 2028.
- The repo has the .NET 10 SDK installed (10.0.203 is the active default), and the EarlyEagles backend has not been scaffolded yet — so there is no migration cost.

Standing up an enterprise B2B SaaS on a runtime that is already out of support is not defensible for a product we intend to sell to other clubs. The plan documents simply pre-date the .NET 10 GA.

## Decision

Target **.NET 10 (`net10.0`)** for the backend instead of .NET 9. Every `csproj` under [`apps/api/`](../../apps/api) declares `<TargetFramework>net10.0</TargetFramework>`. EF Core, ASP.NET Core, and all Microsoft framework packages move to the matching .NET 10 major version.

No other architectural pick changes:

- Clean Architecture layering (Domain / Application / Infrastructure / Api) — unchanged.
- EF Core as the ORM, PostgreSQL via Npgsql provider — unchanged.
- MediatR + FluentValidation + AutoMapper pipeline (P0-023) — unchanged.
- NetArchTest + xUnit + Testcontainers test pyramid — unchanged.
- All Azure picks (Container Apps, Postgres Flexible Server, Service Bus, Redis, Key Vault) — unchanged. Azure Container Apps supports .NET 10 base images out of the box.

## Consequences

### Saved now
- No mid-Phase-0 runtime upgrade ("ship on 9 then migrate to 10") and the wasted work that comes with it.
- LTS support window covers Phase 0 → Phase 4 timeframe without another forced upgrade.

### Cost paid
- Library-compatibility risk: a Phase-0 pick that lacks a .NET 10 target would force a workaround. Audited at scaffold time — Clerk.Net.SDK, MediatR, FluentValidation, AutoMapper, EF Core, NetArchTest.Rules all ship .NET 10–compatible builds.
- Marketing / docs consistency: README, Plan.md, Implementation-Plan.md, and CLAUDE.md still say "ASP.NET Core 9" in several places. These are corrected as part of this ADR's documentation pass (see below).

### Documentation impact

- [`CLAUDE.md`](../../CLAUDE.md) "Operative stack" row "Backend" updated to **ASP.NET Core 10 + EF Core 10**.
- [`Plan.md`](../../Plan.md) stack table and prose updated to .NET 10 wherever .NET 9 appeared.
- [`Implementation-Plan.md`](../../Implementation-Plan.md) Phase 0 task descriptions that mention ".NET 9" updated to ".NET 10".
- [`PROGRESS.md`](../../PROGRESS.md) Phase 0 backend section already reflects the new target.
- ADR-0001 and ADR-0002 stay as-is per the immutable-ADR convention. Their runtime-version cells are now historical, superseded by this ADR.

### Risk acknowledged
If a future Phase 0 pick (e.g. an Azure SDK, a vendor SDK) lags on .NET 10 support, this ADR may need an "exception list" amendment or a downgrade to .NET 9 LTS-equivalent (there is no .NET 9 LTS — the next LTS before 10 was .NET 8, which the project owner explicitly does not want to start on for a new build). Audit at scaffold time showed no such blocker.

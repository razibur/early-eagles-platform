# Developer Walkthrough — Building the Early Eagles Platform from Zero

> **Purpose.** This is a reproducible, step-by-step guide to building the Early Eagles
> platform by hand, in the same order and with the same choices the real repo was built.
> Follow it top to bottom and you will arrive at the current state of the codebase
> (Phase 0, multi-tenancy core complete). Every version and path here is taken from the
> actual repo — when in doubt, the committed files are the source of truth.
>
> **Scope of this document.** It covers everything built through **P0-034.4** (the
> tenant-isolation gate, read and write sides). Later phases (Clerk auth, Stripe, SignalR,
> Azure/Bicep, CI/CD) are **not built yet**; where they belong is flagged in
> [§13 Not-yet-built](#13-not-yet-built-and-where-it-goes) so you know what comes next.
>
> **How to read it.** Prose explains *why*; fenced blocks are commands or file contents
> you can copy. `▶ Reference:` links point at the real file so you can diff your result.
> `⚠ Gotcha:` blocks are traps we actually hit — read them before you hit them too.

---

## Table of contents

1. [What you are building](#1-what-you-are-building)
2. [Prerequisites & toolchain](#2-prerequisites--toolchain)
3. [The monorepo root](#3-the-monorepo-root)
4. [Frontend — Next.js 16 web app](#4-frontend--nextjs-16-web-app)
5. [Backend — Clean Architecture solution](#5-backend--clean-architecture-solution)
6. [Backend — shared build rules & quality gates](#6-backend--shared-build-rules--quality-gates)
7. [Backend — cross-cutting API infrastructure](#7-backend--cross-cutting-api-infrastructure)
8. [Multi-tenancy core](#8-multi-tenancy-core)
9. [Persistence — EF Core, the query filter & migrations](#9-persistence--ef-core-the-query-filter--migrations)
10. [Testing infrastructure](#10-testing-infrastructure)
11. [Running & verifying locally](#11-running--verifying-locally)
12. [Infrastructure — Bicep modules](#12-infrastructure--bicep-modules)
13. [Not-yet-built (and where it goes)](#13-not-yet-built-and-where-it-goes)
14. [Appendix A — version pin table](#appendix-a--version-pin-table)
15. [Appendix B — the gotcha catalogue](#appendix-b--the-gotcha-catalogue)

---

## 1. What you are building

An **enterprise, multi-tenant B2B SaaS** for badminton clubs. The load-bearing facts that
shape every decision below:

- **Multi-tenant from day one** — pool model: one Postgres, `tenant_id` on every domain
  table, EF Core global query filters, subdomain-based tenant resolution, and an automated
  isolation test gate that must stay green before any feature ships.
- **Quality over speed** — it is meant to be sold to other clubs. Warnings are errors,
  analyzers run in the build, architecture rules are enforced by tests.
- **English only for now** — i18n is deliberately deferred ([ADR-0003](./adr/0003-defer-i18n.md)).

Read [`project-scope.md`](../project-scope.md) and [ADR-0002](./adr/0002-enterprise-saas-reframe.md)
(the operative architecture decision) before building — this walkthrough assumes that context.

**Final layout you are working toward:**

```text
early-eagles/                     # monorepo root (npm workspaces)
├── package.json                  # workspaces + dev/build/lint scripts
├── apps/
│   ├── web/                      # Next.js 16 + React 19 + Tailwind v4 + shadcn/ui
│   └── api/                      # ASP.NET Core 10, Clean Architecture
│       ├── EarlyEagles.slnx      # solution (new XML .slnx format)
│       ├── Directory.Build.props # shared TFM, warnings-as-errors, StyleCop
│       ├── .config/dotnet-tools.json   # local dotnet-ef tool
│       ├── src/
│       │   ├── EarlyEagles.Domain/         # entities, value objects, no deps
│       │   ├── EarlyEagles.Application/     # Mediator, FluentValidation, Mapperly
│       │   ├── EarlyEagles.Infrastructure/  # EF Core, tenancy impls
│       │   └── EarlyEagles.Api/             # ASP.NET Core host
│       └── tests/
│           ├── EarlyEagles.Tests.Unit/         # xUnit + NetArchTest
│           ├── EarlyEagles.Tests.Integration/  # WebApplicationFactory + Testcontainers
│           └── EarlyEagles.Tests.Acceptance/   # (scaffold, no tests yet)
├── docs/                         # ADRs, this walkthrough, alternatives, chat-log
└── infra/                        # Bicep (modules only; nothing deployed yet)
    ├── types.bicep               # shared exported types (required tags)
    └── modules/                  # one file per resource kind
```

---

## 2. Prerequisites & toolchain

| Tool | Version | Why | Notes |
|---|---|---|---|
| **.NET SDK** | **10.0.x** (LTS) | backend targets net10.0 ([ADR-0006](./adr/0006-target-dotnet-10-lts.md)) | See ⚠ below |
| **Node.js** | **≥ 22** | Next.js 16 / React 19 | `engines` in root `package.json` |
| **npm** | **11** | workspace manager | ⚠ pnpm was preferred; see below |
| **Docker** | any recent | Testcontainers (integration tests) + local Postgres | daemon must be running |
| **Git** | any | version control | |
| **Azure CLI + Bicep CLI** | az 2.x · Bicep 0.47.x | builds `infra/` | `az bicep install` (see §12) |
| **PowerShell 7** or Git Bash | — | the repo is developed on Windows | commands below work in both unless noted |

Verify:

```pwsh
dotnet --version      # -> 10.0.x
node --version        # -> v22+
npm --version         # -> 11.x
docker version        # server section must respond
az bicep version      # -> Bicep CLI version 0.47.x
```

⚠ **Gotcha — the .NET 10 SDK must actually be installed and on PATH.** On the dev machine
`C:\Program Files\dotnet` initially had only .NET 9 SDKs plus a broken empty `10.0.203`
folder, so `dotnet build` failed with `NETSDK1045: The current .NET SDK does not support
targeting .NET 10.0`. Fix: install the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`,
or the [dotnet-install script](https://dot.net/v1/dotnet-install.ps1) with `-Channel 10.0`).
If you install per-user (into `%LOCALAPPDATA%\Microsoft\dotnet`) it will **not** override a
system `C:\Program Files\dotnet` on PATH — prefer an elevated install into `Program Files`
so your IDE picks it up too.

⚠ **Gotcha — pnpm vs npm.** pnpm was the preferred package manager, but `corepack enable`
requires admin rights on Windows (Node lives in `Program Files`). We fell back to **npm 11
workspaces**. To switch later: run `corepack enable pnpm` from an elevated terminal, add
`"packageManager": "pnpm@<version>"` to the root `package.json`, and convert the workspace
scripts.

---

## 3. The monorepo root

The root is an **npm workspaces** monorepo. Only `apps/*` are workspaces; the .NET solution
lives inside `apps/api` and is managed by `dotnet`, not npm.

```bash
mkdir early-eagles && cd early-eagles
git init
```

Create the root `package.json`:

```json
{
  "name": "early-eagles",
  "version": "0.0.0",
  "private": true,
  "workspaces": ["apps/*"],
  "scripts": {
    "dev": "npm --workspace web run dev",
    "build": "npm --workspace web run build",
    "lint": "npm --workspace web run lint"
  },
  "engines": { "node": ">=22.0.0" }
}
```

▶ Reference: [`package.json`](../package.json)

Add a `.gitignore` covering Node (`node_modules`, `.next`) and .NET (`bin`, `obj`).

**Root commands** (run from the repo root):

```bash
npm install          # install all workspace deps
npm run dev          # start Next dev server (apps/web on :3000)
npm run build        # production build for apps/web
npm run lint         # eslint via next lint
```

---

## 4. Frontend — Next.js 16 web app

> ⚠ **This is not the Next.js in your training data.** Next 16 has breaking changes to
> routing, caching, server components, and middleware. Before writing Next-specific code,
> read the guide in `apps/web/node_modules/next/dist/docs/` or Context7 (`/vercel/next.js`).
> This rule is enforced in [`apps/web/AGENTS.md`](../apps/web/AGENTS.md).

Scaffold under `apps/web`. The result uses **Next 16.2, React 19.2, Tailwind v4, TypeScript,
shadcn/ui (Base UI variant), and Recharts**. The dependency set to reproduce:

```jsonc
// apps/web/package.json — dependencies (exact versions in the real file)
"next": "16.2.6", "react": "19.2.4", "react-dom": "19.2.4",
"@base-ui/react": "^1.5.0",          // shadcn Base UI variant primitive layer
"shadcn": "^4.8.0",                  // component CLI/registry
"recharts": "^3.8.0",               // charts
"class-variance-authority", "clsx", "tailwind-merge", "tw-animate-css",
"lucide-react", "motion"
// devDependencies: tailwindcss ^4, @tailwindcss/postcss ^4, eslint ^9,
//                  eslint-config-next 16.2.6, typescript ^5, @types/*
```

▶ Reference: [`apps/web/package.json`](../apps/web/package.json)

**Key conventions already established:**

- **Charts** = shadcn/ui Charts (Recharts under the hood). Primitives live in
  `apps/web/components/ui/chart.tsx`; feature charts in `apps/web/components/charts/`.
  Chart colors are the CSS variables `--chart-1`…`--chart-5` in `apps/web/app/globals.css`.
- New chart components **must be Client Components** (Recharts requires it) and are composed
  into Server Component pages.
- A sample dashboard (bar / line / area over fake club data) is on `/`.

Run it: `npm run dev` from the root → http://localhost:3000.

---

## 5. Backend — Clean Architecture solution

The backend is **ASP.NET Core 10** in **Clean Architecture**: four source projects with a
strict dependency direction (Domain ← Application ← Infrastructure ← Api) plus three test
projects. It lives entirely under `apps/api`.

```bash
cd apps/api
dotnet new sln --name EarlyEagles --format slnx   # new XML .slnx solution format

# source projects
dotnet new classlib -o src/EarlyEagles.Domain
dotnet new classlib -o src/EarlyEagles.Application
dotnet new classlib -o src/EarlyEagles.Infrastructure
dotnet new web      -o src/EarlyEagles.Api          # minimal-API host

# test projects
dotnet new xunit    -o tests/EarlyEagles.Tests.Unit
dotnet new xunit    -o tests/EarlyEagles.Tests.Integration
dotnet new xunit    -o tests/EarlyEagles.Tests.Acceptance

# add all to the solution (see EarlyEagles.slnx for the /src and /tests folders)
dotnet sln EarlyEagles.slnx add src/**/*.csproj tests/**/*.csproj
```

**Wire the dependency direction with project references only:**

- `Application` → references `Domain`
- `Infrastructure` → references `Application`
- `Api` → references `Application` **and** `Infrastructure`
- Each test project → references the projects it tests

▶ Reference: [`apps/api/EarlyEagles.slnx`](../apps/api/EarlyEagles.slnx)

**The dependency rule is enforced by a test**, not just convention — Domain must not depend
on Application or Infrastructure, Application must not depend on Infrastructure. This uses
**NetArchTest** in the Unit project:

▶ Reference: [`CleanArchitectureRulesTests.cs`](../apps/api/tests/EarlyEagles.Tests.Unit/Architecture/CleanArchitectureRulesTests.cs)

Each source project has an `AssemblyMarker.cs` (an empty public marker type) so tests and DI
can reference the assembly by `typeof(AssemblyMarker).Assembly`.

**Application-layer packages** (per [ADR-0007](./adr/0007-mediator-mapperly-supersede-mediatr-automapper.md) —
`martinothamar/Mediator` + Mapperly replace MediatR + AutoMapper, both MIT-licensed):

```xml
<PackageReference Include="Mediator.Abstractions" Version="3.0.*" />
<PackageReference Include="Mediator.SourceGenerator" Version="3.0.*" PrivateAssets="all" />
<PackageReference Include="FluentValidation" Version="12.1.1" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
<PackageReference Include="Riok.Mapperly" Version="4.3.1" PrivateAssets="all" />
```

▶ Reference: [`EarlyEagles.Application.csproj`](../apps/api/src/EarlyEagles.Application/EarlyEagles.Application.csproj)

---

## 6. Backend — shared build rules & quality gates

Instead of repeating settings in every `.csproj`, a single **`Directory.Build.props`** at
`apps/api/` applies to every project under it. This is what makes the build strict.

Key settings:

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>   <!-- warnings fail the build -->
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<AnalysisLevel>latest-Recommended</AnalysisLevel>
<Deterministic>true</Deterministic>
```

Plus **StyleCop.Analyzers** (`1.2.0-beta.556`, `PrivateAssets=all`) applied repo-wide, with a
shared `stylecop.json` linked into every project.

▶ Reference: [`Directory.Build.props`](../apps/api/Directory.Build.props)

⚠ **Gotcha — `TreatWarningsAsErrors` + StyleCop means every file must be clean.** This shapes
everything downstream: XML-doc crefs must resolve, `using`s must be used (a cref-only `using`
trips `IDE0005`), fields follow the repo's no-underscore camelCase convention, one type per
file (`SA1402`), etc. When you add generated code (EF migrations), check it still builds.

---

## 7. Backend — cross-cutting API infrastructure

Before any tenancy work, the API host (`EarlyEagles.Api`) already wires up several
cross-cutting concerns in `Program.cs`. Build these in this order:

1. **Health & readiness endpoints** — `GET /health` (liveness, custom payload with assembly
   version + uptime) and `GET /ready` (readiness, runs registered `IHealthCheck`s).
   ▶ [`HealthEndpoints.cs`](../apps/api/src/EarlyEagles.Api/Health/HealthEndpoints.cs)
2. **OpenAPI + Swagger UI** — `Microsoft.AspNetCore.OpenApi` generates the doc;
   `Swashbuckle.AspNetCore.SwaggerUI` renders it. Gated to non-Production. Document
   transformers advertise the Clerk bearer scheme and the tenant subdomain contract.
   ▶ [`EarlyEagles.Api.csproj`](../apps/api/src/EarlyEagles.Api/EarlyEagles.Api.csproj), `OpenApi/`
3. **Global exception handling** — RFC 7807/9110 ProblemDetails via `IExceptionHandler`
   implementations (`AddExceptionHandling()` / `UseExceptionHandler()`).
   ▶ [`ExceptionHandling/`](../apps/api/src/EarlyEagles.Api/ExceptionHandling/)
4. **Application + Infrastructure DI** — `AddApplication()` registers the source-generated
   Mediator (scoped) with a pipeline of `[Logging, Audit, Validation, Transaction]` and
   FluentValidation validators; `AddInfrastructure(configuration)` registers tenancy, EF,
   `IAuditService`, and `IUnitOfWork`.
   ▶ [`Application/DependencyInjection.cs`](../apps/api/src/EarlyEagles.Application/DependencyInjection.cs)

`Program.cs` ends with `public partial class Program { }` so the integration test project can
reference it via `WebApplicationFactory<Program>`.

▶ Reference: [`Program.cs`](../apps/api/src/EarlyEagles.Api/Program.cs)

> **Pipeline order is load-bearing.** `Logging → Audit → Validation → Transaction → handler`.
> Audit sits *outside* the transaction — and `AuditService` writes on its own connection —
> so a command that fails or rolls back still leaves a trail of what was attempted and why.
> Transaction is innermost, so a validation failure throws before a transaction is ever
> opened. Audit and Transaction both apply only to `ICommand`s; queries pass straight through.
> ▶ [`Behaviors/`](../apps/api/src/EarlyEagles.Application/Behaviors/),
> [`EfUnitOfWork.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/EfUnitOfWork.cs)

---

## 8. Multi-tenancy core

This is the heart of the platform. Build it in this exact order — each step is independently
testable.

### 8.1 The ambient tenant context

A request-scoped holder of "which tenant am I serving." Split into a **reader** and a
**setter** interface so feature code cannot mutate the tenant mid-request:

- `ITenantContext` (reader: `IsResolved`, `TenantId`, `Slug`) — Application layer.
- `ITenantContextInitializer` (setter: `Initialize(tenantId, slug)`) — Application layer.
- `TenantContext` implements both; registered once as scoped, both interfaces resolving to
  the same instance. Throws on read-before-resolve and double-initialize.

▶ [`ITenantContext.cs`](../apps/api/src/EarlyEagles.Application/Abstractions/Tenancy/ITenantContext.cs) ·
[`TenantContext.cs`](../apps/api/src/EarlyEagles.Infrastructure/Tenancy/TenantContext.cs)

### 8.2 Subdomain extraction

A pure parser: `<slug>.earlyeagles.club` (any environment) and `<slug>.localhost` (local dev)
→ `slug`. Strips ports, lowercases, IPv6-safe, rejects bare/foreign/nested hosts. Format
validation is deferred to the DB lookup.

▶ [`SubdomainExtractor.cs`](../apps/api/src/EarlyEagles.Infrastructure/Tenancy/SubdomainExtractor.cs)

### 8.3 Resolution middleware

`TenantResolutionMiddleware` runs before handlers: extract subdomain → `ITenantLookup` →
`ITenantContextInitializer.Initialize`. **Enforcement is environment-gated:** in Production a
missing subdomain → `400`, an unknown slug → `404` (both ProblemDetails); outside Production
it stays lenient so `*.localhost`/IP dev hosts work. Infra paths (`/health`, `/ready`,
`/openapi`, `/swagger`) are never gated.

▶ [`TenantResolutionMiddleware.cs`](../apps/api/src/EarlyEagles.Api/Tenancy/TenantResolutionMiddleware.cs)

> `ITenantLookup` currently has a `NullTenantLookup` placeholder (returns null for every
> slug). The real EF-backed lookup is deferred until there is a seeded DB — so `/whoami`
> cannot resolve a real tenant yet even with a subdomain. This is expected Phase-0 state.

### 8.4 The `/whoami` proof endpoint

A tiny tenant-scoped endpoint that echoes the resolved `ITenantContext`. It exists purely to
make subdomain→tenant resolution observable end-to-end.

▶ [`TenancyEndpoints.cs`](../apps/api/src/EarlyEagles.Api/Tenancy/TenancyEndpoints.cs)

---

## 9. Persistence — EF Core, the query filter & migrations

### 9.1 The `ITenantScoped` marker

The single source of truth for "which rows are tenant data": `interface ITenantScoped { Guid TenantId { get; } }`.
Applied to every tenant entity. The `Tenant` aggregate is *self-scoped* (`TenantId => Id`);
`TenantMember` exposes a real `TenantId` column.

▶ [`ITenantScoped.cs`](../apps/api/src/EarlyEagles.Domain/Primitives/ITenantScoped.cs)

A **reflection guard test** asserts every concrete `Entity<>` in the Domain assembly
implements the marker — so a new entity that forgets it fails the build rather than leaking
rows across tenants.

▶ [`TenantScopedMarkerTests.cs`](../apps/api/tests/EarlyEagles.Tests.Unit/Tenancy/TenantScopedMarkerTests.cs)

⚠ **Gotcha — composite key + strongly-typed id + EF.** `TenantMember`'s identity is
`(TenantId, ClerkUserId)`. For the query filter to emit a real `tenant_id =` predicate and
for the FK to exist, those must be **independent mapped columns** — but EF can only map a
value-object key to a *single* converted column. Resolution: `TenantMember` keeps
`Entity<TenantMemberId>` (a test requires it) with scalar `TenantId`/`ClerkUserId` as the
real key, and the value-object `Id` is *derived*. To keep `Id` correct for DB-loaded rows
too, we rely on **EF Core constructor binding**: the private ctor's parameters match the
mapped columns, so EF calls it on materialization. This avoids making `Entity<TId>.Id`
virtual (which would trip `CA2214`, "virtual call in constructor").

▶ [`TenantMember.cs`](../apps/api/src/EarlyEagles.Domain/Tenants/TenantMember.cs)

### 9.2 Packages & the DbContext

Add to **Infrastructure**:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
<!-- Pin Relational to match: Npgsql 10.0.3 references 10.0.4, which otherwise
     wins assembly resolution and trips MSB3277. -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.11" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" PrivateAssets="all" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
```

`ApplicationDbContext` snapshots `ITenantContext` in its constructor
(`CurrentTenantId = IsResolved ? TenantId : Guid.Empty`) and in `OnModelCreating` loops every
`ITenantScoped` entity, applying a global query filter
`EF.Property<Guid>(e, col) == CurrentTenantId` (`col` = the `TenantId` column, or the PK for
the self-scoped `Tenant`). Unresolved tenant → `Guid.Empty` → tenant-scoped queries read
nothing rather than leak.

▶ [`ApplicationDbContext.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/ApplicationDbContext.cs) ·
[`Persistence/Configurations/`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/Configurations/)

⚠ **Gotcha — the filter must reference the DbContext instance member, not a captured value.**
The EF model is cached per context type, so if the filter closed over a value it would bake
the first request's tenant into every request. Referencing `CurrentTenantId` (a context
member) makes EF re-evaluate it per query. This is verified by a **cross-instance test** that
asserts two contexts with different tenants produce different SQL.

▶ [`TenantQueryFilterTests.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantQueryFilterTests.cs)

### 9.3 The write-side guard — `TenantStampingInterceptor`

The query filter only protects **reads**. On insert, nothing yet stopped a handler from
writing a row with the wrong `TenantId` — or none at all. A `SaveChangesInterceptor` closes
that half:

```csharp
public sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return base.SavingChanges(eventData, result);
    }
    // SavingChangesAsync does the same — override both.
}
```

`Enforce` walks `ChangeTracker.Entries()`, keeps the `ITenantScoped` ones in
Added/Modified/Deleted state, and resolves the tenant discriminator **the same way the query
filter does** (a `TenantId` property where there is one, the PK for the self-scoped `Tenant`).
Then:

| Situation | Outcome |
|---|---|
| Insert, no tenant id, tenant resolved | **stamped** with the ambient tenant |
| Insert, no tenant id, no tenant resolved | rejected — nothing to stamp from |
| Insert/update/delete naming a different tenant | rejected |
| Update that changes `TenantId` | rejected — rows never move between tenants |
| Any write with an explicit tenant id and no resolved tenant | allowed |

That last row is the write-side twin of `IgnoreQueryFilters()`: migrations, seeding, tenant
provisioning and background jobs run outside a request, so they must name the tenant
themselves. Rejections throw `CrossTenantWriteException` — a bug signal, not a user error,
so it is left unhandled and surfaces as a 500.

The `Tenant` aggregate is exempt on **insert** only: creating a tenant necessarily happens
before that tenant can be ambient. Updates and deletes to it are still constrained.

Register it from `OnConfiguring`, as a **static singleton**:

```csharp
private static readonly TenantStampingInterceptor TenantStamping = new();
private static readonly DomainEventOutboxInterceptor DomainEventOutbox = new();  // see 9.5

protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.AddInterceptors(DomainEventOutbox, TenantStamping);
    base.OnConfiguring(optionsBuilder);
}
```

EF calls `OnConfiguring` however the context was built — DI, the design-time factory, or a
test `new`-ing it — so no caller can construct a context without the guard.

▶ [`TenantStampingInterceptor.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/TenantStampingInterceptor.cs) ·
[`CrossTenantWriteException.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/CrossTenantWriteException.cs)

⚠ **Gotchas in this area:**
- Keep the interceptor **stateless** and read the tenant off the context being saved. A
  per-tenant instance would make each `DbContextOptions` distinct and bloat EF's internal
  service-provider cache (EF warns after ~20 providers).
- `ExecuteUpdate` / `ExecuteDelete` never reach `SaveChanges`, so the interceptor cannot see
  them. They *do* honour the global query filter, so they stay inside the ambient tenant
  unless the caller adds `IgnoreQueryFilters()`.
- Changing a **key** property (like `TenantMember.TenantId`) is only legal while the entry is
  `Added`; EF throws the moment you assign it on a tracked, already-persisted entity. That is
  why the "row moves tenants" rule can't be exercised against `TenantMember` today — it will
  matter for the first entity whose `TenantId` isn't part of the key.

### 9.4 Migration tooling

Install `dotnet-ef` as a **local tool** (reproducible in CI):

```bash
cd apps/api
dotnet new tool-manifest                       # creates .config/dotnet-tools.json
dotnet tool install dotnet-ef --version 10.0.11
```

`ApplicationDbContext`'s constructor needs an `ITenantContext`, which the EF CLI cannot
provide — so add a **design-time factory** that supplies an unresolved `TenantContext` and
reads the connection string from `ConnectionStrings__Postgres` (else a placeholder):

▶ [`ApplicationDbContextFactory.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/ApplicationDbContextFactory.cs)

Generate the first migration (it covers **both** tables, because both entities are already
modelled — EF migrations are model deltas, not one-per-table):

```bash
dotnet ef migrations add InitialCreate \
  --project src/EarlyEagles.Infrastructure \
  --startup-project src/EarlyEagles.Infrastructure \
  --output-dir Persistence/Migrations
```

▶ [`Migrations/`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/Migrations/)

Apply and verify against a throwaway Postgres:

```bash
docker run -d --name ee-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres \
  -e POSTGRES_DB=earlyeagles -p 55432:5432 postgres:17
export ConnectionStrings__Postgres="Host=localhost;Port=55432;Database=earlyeagles;Username=postgres;Password=postgres"
dotnet ef database update --project src/EarlyEagles.Infrastructure --startup-project src/EarlyEagles.Infrastructure
docker exec ee-pg psql -U postgres -d earlyeagles -c "\d tenants"
docker exec ee-pg psql -U postgres -d earlyeagles -c "\d tenant_members"
docker rm -f ee-pg
```

You should see `tenants` (`id` PK, unique `slug`, nullable `branding_*`, enum-as-string
`plan`/`status`) and `tenant_members` (composite PK + FK to `tenants(id) ON DELETE CASCADE`).

⚠ **Gotcha — the connection string is a placeholder.** `AddInfrastructure` falls back to a
hardcoded local connection string when `ConnectionStrings:Postgres` is unset. Real
per-environment configuration/secret management is deferred (P0-033.4 / infra).

### 9.5 Domain events → the outbox

An aggregate announces what it did by raising an `IDomainEvent`; something else has to get
that announcement onto the message bus. Publishing it directly from the handler is the
classic **dual write**: the bus call and the database commit can't both be atomic, so a crash
between them either publishes an event for a state change that rolled back, or commits a
state change nobody hears about. The **outbox** removes the second write — the event is
stored as a row in the *same* transaction as the state, and a background poller (P0-050)
delivers it afterwards.

Three pieces:

1. **`IDomainEventDispatcher`** (Application port) — `void Dispatch(Guid tenantId,
   IReadOnlyCollection<IDomainEvent> events)`. Synchronous and void on purpose: it *stages*,
   it does not publish. No bus call, no round trip, nothing to await.
2. **`OutboxDomainEventDispatcher`** (Infrastructure) — maps each event to an `OutboxMessage`
   and `Add`s it to the `ApplicationDbContext` it was constructed over. It never calls
   `SaveChanges`; the rows ride along with whatever transaction the caller already has open.
   The outbox row reuses `IDomainEvent.Id` as its PK, so a message stays traceable to the
   event and a double dispatch of the same instance fails on the unique key instead of
   publishing twice.
3. **`DomainEventOutboxInterceptor`** — on `SavingChanges`, sweeps `ChangeTracker` for
   `IHasDomainEvents`, dispatches each aggregate's buffer, then clears it. Registered from
   `OnConfiguring` *ahead of* `TenantStampingInterceptor`, so the rows it stages still pass
   under the write-side tenant guard.

`IHasDomainEvents` is a non-generic view of the buffer on `AggregateRoot<TId>` — the sweep
can't name a generic base type, so it matches on that interface instead. Raising stays
`protected`; only the aggregate decides what it announces.

Tenant attribution, in order: the aggregate's own `ITenantScoped.TenantId`, else the ambient
tenant (the same fallback the stamping interceptor applies to un-stamped inserts), else throw
— an outbox row belonging to no tenant is never written.

Note the deliberate contrast with `AuditService`, which opens its **own** context on its own
connection so a rolled-back command still leaves a trail. The audit log wants to outlive a
rollback; the outbox must not.

▶ [`IDomainEventDispatcher.cs`](../apps/api/src/EarlyEagles.Application/Abstractions/Messaging/IDomainEventDispatcher.cs) ·
[`OutboxDomainEventDispatcher.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxDomainEventDispatcher.cs) ·
[`DomainEventOutboxInterceptor.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/DomainEventOutboxInterceptor.cs)

⚠ **Gotchas in this area:**
- Materialise `ChangeTracker.Entries<T>()` before dispatching. Dispatching adds entries to the
  very change tracker you are enumerating.
- Snapshot `DomainEvents` before `ClearDomainEvents()` — it is a read-only *view* over the
  live list, not a copy.
- Serialise the payload against `domainEvent.GetType()`, not the static `IDomainEvent`, or
  every property the event actually carries is silently dropped.
- `OutboxMessage.Create` rejects a non-zero `DateTimeOffset` offset, so the dispatcher
  normalises with `ToUniversalTime()`.

### 9.6 Draining the outbox — `OutboxProcessor`

The dispatcher only ever *writes* rows. `OutboxProcessor` is the `BackgroundService`
that comes along afterwards and reads them back:

```csharp
using var timer = new PeriodicTimer(options.PollInterval);

while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        await DrainOnceAsync(stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
    catch (Exception exception) { OutboxProcessorLog.PollFailed(logger, exception); }
}
```

One drain = one DI scope: read the pending batch, publish each message through
`IOutboxPublisher`, stamp the outcome on the row, save once. The scope spans the whole drain
rather than just the read, because the messages have to stay **tracked** by the context that
will save their stamps.

```csharp
foreach (var message in pending)
{
    try
    {
        await publisher.PublishAsync(message, cancellationToken);
        message.MarkProcessed(DateTimeOffset.UtcNow);   // leaves the pending index
    }
    catch (Exception exception)
    {
        message.RecordFailure(Describe(exception));     // stays pending, attempt counted
    }
}

await context.SaveChangesAsync(cancellationToken);      // one round trip for the batch
```

Three decisions worth keeping:

- **A failing tick must not end the loop.** Nothing restarts a `BackgroundService` whose
  `ExecuteAsync` returned. Let one exception escape and delivery stops for the rest of the
  process lifetime — silently, because there are no further errors to log after the first.
- **`PeriodicTimer` waits *before* its first tick.** A host that starts and stops inside one
  interval never opens a connection, which is what keeps every `WebApplicationFactory` test
  from touching the database it does not have.
- **The read crosses tenants on purpose.** Each tick takes its own DI scope, so its
  `ApplicationDbContext` has no ambient tenant and the query filter would match *nothing*.
  `PollOnceAsync` opts out with `IgnoreQueryFilters()` — one process delivers for every
  tenant. It is safe here because the processor never answers a request; there is no caller
  to leak to.

Two consequences worth stating out loud:

- **Delivery is at-least-once, on purpose.** The row is stamped only *after* a successful
  publish, so a process that dies in between re-sends on the next tick. A lost event is worse
  than a repeated one, so publishers must tolerate a duplicate.
- **One bad message does not hold up the batch.** It stays pending with `AttemptCount` bumped
  and `LastError` recorded, and the drain moves on — which means later messages can be
  delivered ahead of a failing one, so strict ordering is not preserved across a failure.
  Availability is worth more here than order; dead-lettering poison messages comes with the
  real bus.

The query is shaped to the partial index from the outbox migration
(`... ON (occurred_at) WHERE processed_at IS NULL`): pending only, oldest first, capped at
`BatchSize`. Processed rows come to dominate the table, so the working set stays proportional
to the backlog rather than to history.

`OutboxOptions` (`Outbox` section) carries `Enabled`, `PollInterval` and `BatchSize`, bound
with `.Validate(...).ValidateOnStart()` so a bad interval fails the deploy instead of
surfacing later as a background exception nobody is watching. `Enabled: false` is the
operational kill switch: rows keep accumulating, only delivery pauses.

`IOutboxPublisher` is the transport seam. Today it is bound to `LoggingOutboxPublisher`, a
placeholder in the same spirit as `NullTenantLookup`: it logs and reports success, so the whole
publish/stamp path runs on a laptop with no Azure account. **While it is registered, events are
marked processed without reaching any bus.** The real Service Bus implementation waits on the
topic existing (P0-051 → the Bicep work in P0-058.4) and is a registration change, nothing more.

▶ [`OutboxProcessor.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxProcessor.cs) ·
[`OutboxOptions.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxOptions.cs) ·
[`IOutboxPublisher.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/IOutboxPublisher.cs)

⚠ **Gotcha — `StartAsync` does not mean `ExecuteAsync` has run.** It only *schedules* the
body; stopping straight afterwards can cancel it before it ever executes, leaving
`ExecuteTask` in the `Canceled` state with nothing logged. A start/stop pair therefore proves
nothing. Tests here wait for a specific log line before asserting or stopping — which is also
a handshake rather than a sleep, so they stay fast and deterministic.

---

## 10. Testing infrastructure

Three test projects, all xUnit:

- **`Tests.Unit`** — pure domain/behavior tests + the NetArchTest dependency-rule tests +
  the `ITenantScoped` reflection guard. References `Microsoft.Extensions.DependencyInjection`
  and `.Configuration` (for DI-shape tests) and `NetArchTest.Rules`.
- **`Tests.Integration`** — boots the real pipeline via
  `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) for HTTP-level tests,
  and uses **Testcontainers.PostgreSql** (`4.14.0`) for DB tests.
- **`Tests.Acceptance`** — scaffold only, no tests yet.

**The Testcontainers fixture** starts an ephemeral `postgres:17`, applies the real
`InitialCreate` migration, **seeds two tenants (Alpha + Beta) with members each**, and hands
tests an `ApplicationDbContext` bound to it — shared once per collection. `CreateContext(tenantId, slug)`
gives a context that "operates as" a tenant. This is the tenant-**isolation gate** (P0-034),
which CLAUDE.md calls *the gate* that must stay green before any feature ships.

The gate proof (`TenantIsolationTests`) asserts, against real Postgres, that reading as Alpha
returns only Alpha's rows, a direct PK lookup of Beta's row as Alpha returns null (the filter
is not key-bypassable), `Tenant` is self-scoped, and an `IgnoreQueryFilters()` backstop shows
Beta's data really is in the DB — the filter is what hides it.

The write-side proof (`TenantWriteStampingTests`, 9 tests) is the mirror image: an insert with
no tenant id is stamped with the ambient one and lands where Alpha — and only Alpha — can
read it; inserts, updates and deletes naming another tenant are refused with
`CrossTenantWriteException`; and the deliberate out-of-band paths (explicit tenant id with no
resolved tenant, provisioning a brand-new tenant) still work. Disabling the interceptor
registration fails 6 of the 9 — the three "is allowed" cases stay green, which is how you
know they are not just asserting the guard.

▶ [`PostgresFixture.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresFixture.cs) ·
[`PostgresCollectionDefinition.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresCollectionDefinition.cs) ·
[`TenantIsolationTests.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantIsolationTests.cs) ·
[`TenantWriteStampingTests.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantWriteStampingTests.cs)

⚠ **Gotchas in this area:**
- xUnit **v2** (`2.9.3`) `IAsyncLifetime` returns `Task`, not `ValueTask` (that's v3).
- `CollectionDefinition` classes trip `CA1711` ("don't end a type name in 'Collection'") —
  name it `...CollectionDefinition` instead, and keep one type per file (`SA1402`).
- The `PostgreSqlBuilder()` parameterless ctor is obsolete in Testcontainers 4.14 — use
  `new PostgreSqlBuilder("postgres:17")`.
- Integration/Testcontainers tests **require a running Docker daemon** (locally and in CI).

Run everything:

```bash
cd apps/api
dotnet test EarlyEagles.slnx
```

---

## 11. Running & verifying locally

**Backend** (boots without a database — the DbContext is lazy and nothing queries it yet):

```bash
dotnet run --project apps/api/src/EarlyEagles.Api --urls http://localhost:5080
```

```bash
curl http://localhost:5080/health   # {"status":"Healthy","version":"1.0.0+<sha>","uptimeSeconds":..}
curl http://localhost:5080/ready    # {"status":"Healthy","checks":[],...}
curl http://localhost:5080/whoami   # 400 "Tenant not resolved" (no subdomain; dev is lenient)
```

`/whoami` returns `400` even with an `early-eagles.localhost` Host header, because
`NullTenantLookup` resolves nothing yet — expected until the EF-backed lookup + seed land.
In non-Production, `/swagger` serves the API docs.

**Frontend:** `npm run dev` from the repo root → http://localhost:3000 (sample dashboard).

---

## 12. Infrastructure — Bicep modules

Read [`infra-conventions.md`](./infra-conventions.md) first — names, tags, region and SKUs are
decided there, and an Azure resource cannot be renamed later. Modules are written and
`bicep build`-checked locally; **nothing is deployed** until P0-059.3.

Install the Bicep CLI once (it lives under `~/.azure/bin`, no admin needed):

```bash
az bicep install
az bicep version
```

### 12.1 Shared types — `infra/types.bicep`

The five required tags (§5 of the conventions) are an exported **user-defined type**, so a
module whose `tags` parameter is typed `requiredTags` refuses to compile when a tag is
missing or `env` is not `dev`/`stg`/`prod`. A `*: string` index signature lets the optional
tags (`dataClassification`, `criticality`) through.

```bicep
@export()
type requiredTags = {
  env: environmentCode          // 'dev' | 'stg' | 'prod'
  workload: 'early-eagles'
  owner: string
  costCenter: string
  managedBy: 'bicep'
  *: string
}
```

▶ Reference: [`infra/types.bicep`](../infra/types.bicep)

### 12.2 `resourceGroup.bicep` (P0-057.1)

Resource groups exist at **subscription** scope, so the module sets
`targetScope = 'subscription'` and is called from the (future) subscription-scoped
`main.<env>.bicep`. It takes `name`, `location` and `tags` and hard-codes none of them —
the caller builds `rg-ee-<env>-<region>`. It outputs `name` and `id` so the next modules can
be deployed with `scope: resourceGroup(rg.outputs.name)`.

```bicep
targetScope = 'subscription'
import { requiredTags } from '../types.bicep'

param name string
param location string
param tags requiredTags

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: name
  location: location
  tags: tags
}
```

▶ Reference: [`infra/modules/resourceGroup.bicep`](../infra/modules/resourceGroup.bicep)

Verify:

```bash
az bicep build --file infra/modules/resourceGroup.bicep --stdout > /dev/null   # no output = no warnings
```

### 12.3 `keyVault.bicep` (P0-057.2)

Resource-group scoped. Takes `name`, `location`, `tags` and `skuName`; the caller builds the
globally-unique name per conventions §3.1:

```bicep
var unique = take(uniqueString(subscription().subscriptionId, environment), 5)
var kvName = 'kv-ee-${environment}-eus-${unique}'   // prod: 20 chars, limit 24
```

Three settings are fixed in the module rather than parameterised, because they must hold in
every environment: `enableRbacAuthorization: true` (access via role assignments to managed
identities in P0-062.3, not vault access policies), `enableSoftDelete: true`, and
`enablePurgeProtection: true`. Outputs are `name`, `id` (the scope for role assignments) and
`uri` (what the API is configured with).

The calling module deploys it into the group from §12.2:

```bicep
module kv 'modules/keyVault.bicep' = {
  scope: resourceGroup(rgName)
  dependsOn: [ rg ]
  params: { name: kvName, location: location, tags: tags, skuName: 'standard' }
}
```

▶ Reference: [`infra/modules/keyVault.bicep`](../infra/modules/keyVault.bicep)

⚠ **Gotcha — purge protection is one-way, and deleted vault names stay reserved.** Once a
vault has purge protection it can never be turned off, and after deletion the vault (and its
name) sits in soft-deleted state for `softDeleteRetentionInDays` (default 90) and **cannot be
purged early**. Redeploying the same name during that window fails; recover it instead
(`az keyvault recover --name <vault>`). This is why the vault name comes from a deterministic
`uniqueString` rather than a random suffix — a teardown/redeploy lands on the same name and
recovery is the fix, not a new vault.

### 12.4 `logAnalytics.bicep` (P0-057.3)

Resource-group scoped, one per environment (`log-ee-<env>-<region>`, no instance suffix).
Takes `name`, `location`, `tags`, `skuName` (`PerGB2018`), `retentionInDays` (30–730) and
`dailyQuotaGb`. Outputs `name`, `id` (linked by App Insights and the Container Apps
environment) and `customerId`.

The cap is the point of this module. `dailyQuotaGb` is required and has `@minValue(1)`, so
a caller cannot omit it or pass Azure's `-1` ("unlimited") — the build fails:

```bicep
@minValue(1)
param dailyQuotaGb int          // no default: every environment must choose
```

`enableLogAccessUsingOnlyResourcePermissions: true` makes access follow the resource that
sent the log, so granting read on one Container App does not expose every table.

▶ Reference: [`infra/modules/logAnalytics.bicep`](../infra/modules/logAnalytics.bicep)

⚠ **Gotcha — hitting the daily cap drops data, it doesn't queue it.** When the cap is
reached, ingestion stops until the daily reset and everything sent in between is discarded
(the cap is also enforced with some lag, so a spike can overshoot it). That is the trade:
a bounded bill for a possible telemetry gap during an incident. Size the cap with headroom
over normal volume, and add an alert on the workspace's cap-reached event when the
alerting work lands (P0-094).

### 12.5 `appInsights.bicep` (P0-057.4)

Resource-group scoped, one per environment (`appi-ee-<env>-<region>`). **Workspace-based**:
it takes the Log Analytics workspace's resource ID and stores telemetry there, so retention
and the daily cap from §12.4 apply — there is no second setting to keep in sync. Passing
the workspace output also gives Bicep the deploy ordering for free:

```bicep
module appi 'modules/appInsights.bicep' = {
  scope: resourceGroup(rgName)
  params: { name: 'appi-ee-${environment}-eus', location: location, tags: tags,
            workspaceId: log.outputs.id }   // implicit dependsOn: log
}
```

The connection string is a `@secure()` output (compiled type `securestring`) so it is kept
out of deployment history and `az deployment … show`. It only permits *sending* telemetry,
but there is no reason to publish it. `disableLocalAuth` defaults to `false`; turn it on
once the OpenTelemetry exporter (P0-089.1) authenticates with the managed identity, after
which the connection string alone is no longer enough to ingest.

▶ Reference: [`infra/modules/appInsights.bicep`](../infra/modules/appInsights.bicep)

### 12.6 `postgresFlexible.bicep` (P0-058.1)

Resource-group scoped (`psql-ee-<env>-<region>-001`). One module creates four things: the
server, its Entra ID administrator, the `earlyeagles` application database, and firewall
rules. Everything that differs between dev and prod is a parameter — `skuName`/`skuTier`,
`storageSizeGB` (≥ 32), `backupRetentionDays` (7–35), `geoRedundantBackup` and
`highAvailabilityMode` — so the prod HA decision lives in `main.prod.bicep`, not here.

Fixed in the module:

- **Entra-only auth.** `passwordAuth: 'Disabled'`, so there is no admin password to pass in
  (and none to leak from a parameter file). The admin is an Entra user/group/service
  principal (`entraAdmin*` params). To connect with `psql`, use a token as the password:

  ```bash
  export PGPASSWORD=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)
  psql "host=<fqdn> dbname=earlyeagles user=<entra-admin-upn> sslmode=require"
  ```

- **`dataClassification: confidential`** is unioned into the caller's tags.
- **Maintenance window** defaults to Tuesday 07:00 UTC (03:00 US Eastern) — Sunday is when
  the club plays.
- **Postgres 17**, matching the `postgres:17` image used locally and by Testcontainers.

`allowAzureServices` (the `0.0.0.0` "Allow Azure services" rule) defaults **off**: it admits
*any* Azure customer's resources, not just ours. `firewallRules` is a typed array for
operator IPs. Rules deploy with `@batchSize(1)` because the server rejects concurrent
firewall-rule writes.

▶ Reference: [`infra/modules/postgresFlexible.bicep`](../infra/modules/postgresFlexible.bicep)

⚠ **Gotcha — Bicep does not validate the Postgres `version` against the API.** API
`2024-08-01` only accepts versions 11–16, but a template sending `'17'` (or `'99'`) to it
builds cleanly and fails only at deploy time. `2025-08-01` is the first GA API that accepts
17. The module's own `@allowed(['16', '17'])` is the only compile-time guard — check the
[ARM reference](https://learn.microsoft.com/azure/templates/microsoft.dbforpostgresql/flexibleservers)
before bumping either the version or the API.

⚠ **Gotcha — some choices are create-time only or cross-parameter.** `geoRedundantBackup`
can only be set when the server is created, and storage can grow but never shrink. And
`skuTier: 'Burstable'` with any HA mode other than `Disabled` builds fine but is rejected
at deploy — Bicep can't check one parameter against another.

⚠ **Gotcha — module paths must be relative.** A `module x 'D:/…/resourceGroup.bicep'`
reference fails with `BCP189: module reference scheme "D" is not recognized` — Bicep reads
the drive letter as a registry scheme (`br:`, `ts:`). Always reference modules relative to
the calling file.

---

## 13. Not-yet-built (and where it goes)

Everything below is **planned, not built** — see [`Implementation-Plan.md`](../Implementation-Plan.md)
for the task breakdown and [`Plan.md`](../Plan.md) for the full stack/roadmap.

| Area | Status | Where it plugs in |
|---|---|---|
| **Service Bus transport** (P0-050.2.2) | blocked | the drain publishes + stamps through `IOutboxPublisher`; the real publisher waits on the topic (P0-051 → P0-058.4) |
| **EF-backed `ITenantLookup`** | deferred | replaces `NullTenantLookup` once a seeded DB exists |
| **Audit query endpoint** (P0-048) | not started | tenant-admin-scoped, paginated read over `audit_events` |
| **Clerk auth** (B2B Orgs = tenants) | Phase 0/2 | JWT bearer, `[Authorize]`, webhook → tenant provisioning |
| **Stripe Billing** | Phase 2 | tenant *subscription* billing only — **not** member dues |
| **SignalR / Redis / Service Bus** | later phases | realtime, cache/locks, messaging |
| **Cloud + IaC** | conventions written | **Azure** + **Bicep** (`infra/`), per [ADR-0002](./adr/0002-enterprise-saas-reframe.md). Naming, tagging, region and SKU rules are settled in [`infra-conventions.md`](./infra-conventions.md); the P0-057 modules (resource group, Key Vault, Log Analytics, App Insights), the Postgres module (P0-058.1) and the shared tag type exist (§12), the remaining modules and `main.<env>.bicep` do not, and nothing is deployed. |
| **CI/CD** | not started | GitHub Actions (dev → staging → prod, manual prod promote) |

**On Vercel specifically:** the operative stack ([ADR-0002](./adr/0002-enterprise-saas-reframe.md))
targets **Azure**, not Vercel. The Next.js frontend *could* deploy to Vercel, but that is not
the chosen path and no Vercel config exists in the repo. If a future ADR adopts Vercel for the
web tier, document it there first — **do not** introduce a deployment target without an ADR.

---

## Appendix A — version pin table

Exact versions currently in the repo (source of truth = the `.csproj`/`package.json` files):

| Package / tool | Version |
|---|---|
| .NET SDK / runtime | 10.0.x (LTS) |
| ASP.NET Core OpenAPI | 10.0.7 |
| Swashbuckle.AspNetCore.SwaggerUI | 10.2.1 |
| Microsoft.OpenApi | 2.7.5 (pinned over a transitive CVE) |
| Mediator.Abstractions / SourceGenerator | 3.0.* |
| FluentValidation (+ DI ext) | 12.1.1 |
| Riok.Mapperly | 4.3.1 |
| Microsoft.EntityFrameworkCore (+ Relational, Design) | 10.0.11 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| Microsoft.Extensions.Hosting.Abstractions / Options.ConfigurationExtensions | 10.0.11 |
| dotnet-ef (local tool) | 10.0.11 |
| StyleCop.Analyzers | 1.2.0-beta.556 |
| NetArchTest.Rules | 1.3.2 |
| xunit / runner | 2.9.3 / 3.1.4 |
| Microsoft.NET.Test.Sdk | 17.14.1 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 |
| Testcontainers.PostgreSql | 4.14.0 |
| Next.js / React | 16.2.6 / 19.2.4 |
| Tailwind CSS | v4 |
| Node.js | ≥ 22 · npm 11 |
| Postgres (dev/test image) | postgres:17 |
| Bicep CLI | 0.47.16 |
| `Microsoft.Resources/resourceGroups` API | 2024-03-01 |
| `Microsoft.KeyVault/vaults` API | 2024-11-01 |
| `Microsoft.OperationalInsights/workspaces` API | 2023-09-01 |
| `Microsoft.Insights/components` API | 2020-02-02 |
| `Microsoft.DBforPostgreSQL/flexibleServers` (+ children) API | 2025-08-01 |
| Azure Postgres major version | 17 |

---

## Appendix B — the gotcha catalogue

A quick index of the traps documented above, for when you hit the symptom:

| Symptom | Cause | Fix |
|---|---|---|
| `NETSDK1045: does not support targeting .NET 10.0` | .NET 10 SDK missing / broken install | install SDK 10.0.x into `Program Files` (elevated) |
| `corepack enable` fails on Windows | Node in `Program Files` needs admin | use npm 11, or enable pnpm from an elevated shell |
| `MSB3277` EF Core Relational version conflict | Npgsql 10.0.3 references Relational 10.0.4 | pin `Microsoft.EntityFrameworkCore.Relational` 10.0.11 |
| `CA2214` when deriving `Id` from a composite key | virtual member called in base ctor | keep scalars as the key; use EF **constructor binding** for `Id` |
| Query filter uses the wrong tenant across requests | filter captured a value; model is cached | reference the DbContext member (`CurrentTenantId`) in the filter |
| `CA1711` on a `*Collection` xUnit class | reserved suffix | rename to `*CollectionDefinition` |
| xUnit `IAsyncLifetime` signature mismatch | v2 uses `Task`, not `ValueTask` | return `Task` |
| Generated EF migration breaks the strict build | warnings-as-errors + StyleCop | our migrations happen to be clean; if not, mark them generated |
| `/whoami` returns 400 even with a subdomain | `NullTenantLookup` resolves nothing yet | expected until the EF-backed lookup + seed land |
| Package downgrade error after adding EF | test project pinned an older `Microsoft.Extensions.*` | bump the transitive pin to match (10.0.11) |
| Domain events never reach the outbox | payload serialised as `IDomainEvent`, or the buffer cleared before it was read | serialise the runtime type; snapshot `DomainEvents` first |
| `BackgroundService` test fails only in a full run | `StartAsync` schedules `ExecuteAsync`; stopping first cancels it before the body runs | wait for a log-line signal before asserting or stopping |
| `az bicep build` → `Bicep CLI not found` | Bicep is not bundled with az | `az bicep install` |
| `BCP189: module reference scheme "D" is not recognized` | absolute Windows path in a `module` reference | reference modules by relative path |
| Key Vault redeploy fails: name already in use | previous vault is soft-deleted and purge-protected | `az keyvault recover --name <vault>` |
| Logs stop arriving partway through the day | Log Analytics daily cap reached; ingestion paused until reset | expected; raise `dailyQuotaGb` if normal volume hits it |
| Postgres deploy rejects `version: '17'` though Bicep built it | API version older than `2025-08-01` | use `2025-08-01`+; Bicep doesn't validate the enum |
| Background poller reads zero rows forever | its scope has no ambient tenant, so the query filter matches nothing | `IgnoreQueryFilters()` on the drain query |

---

*Keep this document current: when a build step, version, or setup gotcha changes, update the
relevant section and Appendix A/B in the same change. See [PROGRESS.md](../PROGRESS.md) for
live task status.*

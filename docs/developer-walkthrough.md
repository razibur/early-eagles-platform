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

### 4.1 The service worker — no PWA plugin (P0-067.1)

The PWA layer has **no plugin** ([ADR-0011](./adr/0011-hand-written-service-worker-instead-of-next-pwa.md)).
`next-pwa` hasn't been released since 2022 and only hooks into webpack, while Next 16 builds
with Turbopack. The Next.js 16 PWA guide (`node_modules/next/dist/docs/01-app/02-guides/progressive-web-apps.md`)
does it by hand, and so do we. Three pieces:

1. **`apps/web/public/sw.js`** — served as-is from `/sw.js`. It calls `skipWaiting()` on
   install and `clients.claim()` on activate, and nothing else: **no `fetch` handler, no
   caches**, so every request goes to the network and a deploy can't be hidden behind a stale
   cache. Push handlers join it in P0-068.2.
2. **`apps/web/components/pwa/service-worker-registration.tsx`** — a Client Component that
   registers `/sw.js` in a `useEffect` with `scope: "/"` and `updateViaCache: "none"`, and
   renders nothing. It sits at the end of `<body>` in `app/layout.tsx`.
3. **`apps/web/next.config.ts`** — an `async headers()` rule for `/sw.js`:
   `Cache-Control: no-cache, no-store, must-revalidate`, an explicit JavaScript
   `Content-Type`, and `Content-Security-Policy: default-src 'self'; script-src 'self'`.

▶ Reference: [`public/sw.js`](../apps/web/public/sw.js),
[`service-worker-registration.tsx`](../apps/web/components/pwa/service-worker-registration.tsx),
[`next.config.ts`](../apps/web/next.config.ts)

Check it: `npm run dev`, open http://localhost:3000, then DevTools → **Application → Service
workers** shows `/sw.js` *activated and running* for scope `/`. In the console,
`navigator.serviceWorker.controller` is non-null on the **first** visit (that's
`clients.claim()`). `curl -I http://localhost:3000/sw.js` shows the three headers.

> ⚠ **Gotcha — a cached worker outlives the deploy.** Without `updateViaCache: "none"` *and*
> `no-cache` on `/sw.js`, the browser can keep checking against a cached copy of the old worker,
> and users run it until the cache expires. Keep both.

> ⚠ **Gotcha — "cleaning up" with `skipWaiting` once caching exists.** Taking over immediately
> is safe only because the worker caches nothing. If offline precaching is added later (Serwist,
> per ADR-0011), revisit it: a new worker taking over a page that loaded old assets can serve a
> mismatched mix.

> ⚠ **Gotcha — CSS-only properties in a Recharts `tick` object (P0-067.4).** A chart's `tick`
> object becomes SVG attributes on `<text>`. `text-transform` is CSS, not an SVG attribute, so
> `tick={{ textTransform: "uppercase" }}` fails the type check (and `npm run build`) and would be
> ignored by the browser anyway. Put it in `style`:
> `tick={{ fill: …, fontSize: 11, style: { textTransform: "uppercase" } }}`. `npm run dev`
> doesn't type-check, so this only shows up at build time.

### 4.2 Manifest and icons (P0-067.2)

The manifest is Next's built-in metadata route, **`app/manifest.ts`**, served at
`/manifest.webmanifest` and linked from every page automatically. Name *Early Eagles — Club
HQ*, short name *Early Eagles*, `display: "standalone"`, and `theme_color` / `background_color`
both `#080d11`, the dark theme's `--background`. The root layout repeats that color as
`viewport.themeColor` (Next 16 keeps it in the `viewport` export, not `metadata`) and sets
`metadata.appleWebApp` for iOS, which ignores most of the manifest.

**Icons are generated, not hand-exported.** There's no club logo in the repo yet (the masthead's
logo slot is empty), so the mark is a placeholder: a jersey-block **"EE"** slanted forward over
the masthead's green/red accent strike. The letters are plain rectangles, so no font file is
involved and the mark holds up at 16px. One script draws everything from the same geometry, and
converts the colors from the OKLCH tokens in `globals.css`:

```pwsh
node apps/web/scripts/generate-icons.mjs   # from the repo root
```

| File | Size | Used by |
|---|---|---|
| `app/icon.svg` | vector | modern browser tabs (`<link rel="icon" type="image/svg+xml">`, added by Next) |
| `app/favicon.ico` | 16 / 32 / 48 | older browsers; replaces the Next default |
| `app/apple-icon.png` | 180 | iOS home screen (`apple-touch-icon`, added by Next); full-bleed, iOS rounds it |
| `public/icons/icon-192.png`, `icon-512.png` | 192 / 512 | manifest, `purpose: "any"`; rounded corners |
| `public/icons/icon-maskable-512.png` | 512 | manifest, `purpose: "maskable"`; full-bleed, mark scaled into the central 80% |

Rendering uses **sharp**, which Next already installs for image optimisation, so there's no new
dependency. When the real logo arrives, change the drawing in the script and re-run it; the
file names and the manifest stay the same.

▶ Reference: [`app/manifest.ts`](../apps/web/app/manifest.ts),
[`scripts/generate-icons.mjs`](../apps/web/scripts/generate-icons.mjs),
[`app/layout.tsx`](../apps/web/app/layout.tsx)

Check it: `npm run build && npm --workspace web run start`, then DevTools → **Application →
Manifest**: no errors or warnings, the three icons render, and Chrome offers *Install*.

> ⚠ **Gotcha — "Page is in an incognito window".** Installability checks fail in a private or
> automation profile (`in-incognito`). Test installing in a normal profile.

> ⚠ **Gotcha — `black-translucent` hides the masthead on iOS.** That status-bar style draws the
> page *under* the iOS status bar. Nothing pads for `env(safe-area-inset-top)` yet, so the
> layout uses `black`. Switch only together with safe-area padding.

> ⚠ **Gotcha — one manifest for every tenant.** Name and icons are the Early Eagles ones for
> all clubs. Per-tenant manifests (each club's name and icon) need a request-aware
> `manifest.ts` and belong with tenant branding, later.

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

   The one readiness check today is the database (P0-026.2):
   [`PostgresHealthCheck`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/PostgresHealthCheck.cs)
   runs `SELECT 1` through the app's own `NpgsqlDataSource`, so in Azure it also proves the Entra
   token works. `Program.cs` registers it with a **5-second timeout**:
   `AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", timeout: TimeSpan.FromSeconds(5))`.
   The check lives in Infrastructure, which references only
   `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`; the API gets the rest from ASP.NET.
   A down database makes `/ready` return **503** while `/health` stays 200, so the platform stops
   routing traffic to the replica but doesn't restart it.

   ⚠ **Gotcha — `/ready` is public.** Health-check descriptions are written to the response, and
   the endpoint has no auth. Return fixed text (`"Database unreachable."`) and attach the exception
   to the result for logs. An exception message would leak host names and auth errors.

   ⚠ **Gotcha — Npgsql's connect timeout is 15 s.** Without the registration timeout, a probe
   against an unresponsive host hangs past the platform's probe deadline.
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

`IOutboxPublisher` is the transport seam. Which implementation is bound depends on configuration
(P0-050.2.2):

| Configuration | Publisher | Signs in with |
|---|---|---|
| `ServiceBus:FullyQualifiedNamespace` (Azure; `environment.bicep` sets it where Service Bus exists) | `ServiceBusOutboxPublisher` | the app's `TokenCredential` (managed identity). The namespace has SAS keys disabled |
| `ConnectionStrings:ServiceBus` (the local emulator) | `ServiceBusOutboxPublisher` | the emulator's fixed SAS key |
| neither | `LoggingOutboxPublisher` | — |

`ServiceBusOutboxPublisher` maps each row onto the P0-051 contract (§12.9): `MessageId` = row id,
`Subject` = short event name, `EventType` / `TenantId` properties, JSON body. It sends to
`ServiceBus:TopicName` (default `domain-events`) through **one `ServiceBusClient` and one
`ServiceBusSender` for the process**. Both are thread-safe and own the AMQP connection, so
creating them per request would reconnect every time. A failed send throws, and the drain records
it on the row and retries next tick.

The logging fallback is a placeholder in the same spirit as `NullTenantLookup`: it logs and
reports success, so the publish/stamp path runs on a laptop with no bus. **While it is bound,
events are marked processed without reaching any bus.** Point a local API at the emulator with:

```pwsh
$env:ConnectionStrings__ServiceBus = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
```

⚠ **Gotcha — the emulator's connection string goes nowhere near Azure.** The namespace in Azure
has local auth disabled, so a SAS connection string fails there by design. Azure gets the
namespace name only, and the identity does the rest.

▶ [`OutboxProcessor.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxProcessor.cs) ·
[`OutboxOptions.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxOptions.cs) ·
[`IOutboxPublisher.cs`](../apps/api/src/EarlyEagles.Infrastructure/Outbox/IOutboxPublisher.cs)

⚠ **Gotcha — `StartAsync` does not mean `ExecuteAsync` has run.** It only *schedules* the
body; stopping straight afterwards can cancel it before it ever executes, leaving
`ExecuteTask` in the `Canceled` state with nothing logged. A start/stop pair therefore proves
nothing. Tests here wait for a specific log line before asserting or stopping — which is also
a handshake rather than a sleep, so they stay fast and deterministic.

---

### 9.7 Signing in to Azure Postgres with a token (P0-062.2.1)

Azure Postgres has password auth switched off (§12.6), so in Azure the API signs in with a
**Microsoft Entra access token** for its managed identity (§12.18). Locally nothing changes: the
connection string's password still works.

```jsonc
// appsettings / env — Azure only
"ConnectionStrings": { "Postgres": "Host=psql-ee-dev-eus-001.postgres.database.azure.com;Database=earlyeagles;Username=id-ee-api-dev-eus;SslMode=Require" },
"Postgres": { "Authentication": "Entra" }   // default "Password"
```

`AddInfrastructure` registers **one `NpgsqlDataSource`** built by `PostgresDataSourceFactory`, and
`AddDbContext` uses it. In Entra mode the data source gets a periodic password provider. Every
5 minutes it asks the DI `TokenCredential` (`DefaultAzureCredential`) for a token with scope
`https://ossrdbms-aad.database.windows.net/.default`, and that token is the password.

Why build the data source ourselves: EF's `UseNpgsql(cs, o => o.ConfigureDataSource(...))` is
*untracked*, meaning EF doesn't account for the callback when it caches data sources. Npgsql's
docs say to pass a pre-built data source instead.

Entra mode fails fast at startup if the connection string has a `Password` (it would be
silently ignored) or no `Username` (Azure matches the token to a Postgres role by name).

To try it from a laptop against a real server, run `az login`, set `Postgres__Authentication=Entra`,
and set a connection string with your UPN as `Username`. `DefaultAzureCredential` picks up the CLI
login.

▶ Reference: [`PostgresDataSourceFactory.cs`](../apps/api/src/EarlyEagles.Infrastructure/Persistence/PostgresDataSourceFactory.cs)

⚠ **Gotcha — don't cache the token for an hour.** The common sample refreshes every 55 minutes.
The credential only renews a token in its last ~5 minutes, so Npgsql could hold one that expires
partway through that hour, and new connections would fail until the next refresh. A 5-minute
refresh is cheap because the credential serves its in-memory cache.

⚠ **Gotcha — overriding the database in a test.** Re-registering `AddDbContext(o => o.UseNpgsql(cs))`
no longer replaces the connection: EF 10 composes every `AddDbContext` configuration, and the
original one points at the DI data source. Replace the `NpgsqlDataSource` singleton instead
(`CrossTenantEndpointTests` does).

### 9.8 Reading the audit log — the first Mediator query (P0-048.1)

`GET /audit-events?limit=50&cursor=…` returns the ambient tenant's audit log, newest first. It is
the first endpoint that goes through Mediator, so it sets the pattern for reads:

| Layer | File | Role |
|---|---|---|
| Application | `Auditing/ListAuditEventsQuery.cs` (+ `…Validator`, `…Handler`) | `IQuery<AuditEventPage>`; limit 1–100, cursor must decode |
| Application | `Abstractions/Auditing/IAuditLogReader.cs` | the port, so the handler never sees EF |
| Infrastructure | `Auditing/EfAuditLogReader.cs` | EF implementation over the request's `ApplicationDbContext` |
| API | `Auditing/AuditEventsEndpoints.cs` | resolves the tenant, sends the query, returns the page |

Because it is an `IQuery`, `AuditBehavior` and `TransactionBehavior` let it pass: reading the log
doesn't write to it. There is **no tenant parameter anywhere** on the read path; the global query
filter (§9.2) scopes every page, and the integration tests prove it with two tenants.

**Keyset paging.** Each page returns `nextCursor`, an opaque base64url token holding the last
row's `(OccurredAt, Id)`. The next page is everything strictly older:

```csharp
events.Where(e => EF.Functions.LessThan(
    ValueTuple.Create(e.OccurredAt, e.Id),
    ValueTuple.Create(cursor.OccurredAt, cursor.Id)))
  .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
```

Npgsql turns that into a Postgres row-value comparison, `(occurred_at, id) < (@t, @id)`, which
follows the existing `(tenant_id, occurred_at DESC)` index. Offset paging would get slower as the
log grows and skip or repeat rows when new events arrive between pages. The handler fetches
`limit + 1` rows to learn whether another page exists, so it never needs a `COUNT`.

**Not in Production yet.** The task says *tenant-admin only*, and admin role claims come from
Clerk (P0-038). Until P0-048.2 adds that policy, `Program.cs` maps the endpoint only outside
Production, and a test pins the 404 in Production.

▶ Reference: [`ListAuditEventsQueryHandler.cs`](../apps/api/src/EarlyEagles.Application/Auditing/ListAuditEventsQueryHandler.cs),
[`EfAuditLogReader.cs`](../apps/api/src/EarlyEagles.Infrastructure/Auditing/EfAuditLogReader.cs),
[`AuditEventsEndpointTests.cs`](../apps/api/tests/EarlyEagles.Tests.Integration/Auditing/AuditEventsEndpointTests.cs)

⚠ **Gotcha — Mediator scans nothing if `Assemblies` is a variable.** The source generator reads
`options.Assemblies` from the *syntax* at compile time. `[typeof(AssemblyMarker)]` works.
`[applicationAssembly]` compiles fine but registers no handlers, and the first `Send` throws
`MissingMessageHandlerException` at runtime. `AddApplication` shipped with the variable form and
nobody noticed until this first handler.

⚠ **Gotcha — `FindAsync` obeys the tenant filter.** With no tenant resolved, the filter hides
every row, so `db.Tenants.FindAsync(id)` returns `null` for a row that exists. Test seeding that
checks "already there?" must use `IgnoreQueryFilters()`.

⚠ **Gotcha — sort uuids in the database, not in .NET.** `Guid.CompareTo` and Postgres `uuid`
order differ. The tie-break is valid only because the ordering *and* the cursor comparison both
run in SQL. Tests compute the expected order with a SQL `ORDER BY`.

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

Dev runs entirely on your machine ([ADR-0009](./adr/0009-local-dev-no-cloud-dev-environment.md));
there is no cloud dev environment. [`docker-compose.yml`](../docker-compose.yml) at the repo root is
the local stack (P0-059.5):

```bash
docker compose up -d                  # Postgres 17 on localhost:55432 (db earlyeagles, postgres/postgres)
docker compose ps                     # postgres should report (healthy)
docker compose down                   # stop; data survives in the pgdata volume
docker compose down -v                # stop and wipe the database
```

It's published on **55432**, not 5432. Point the API (and `dotnet ef`) at it:

```pwsh
$env:ConnectionStrings__Postgres = "Host=localhost;Port=55432;Database=earlyeagles;Username=postgres;Password=postgres"
```

Without that variable the API falls back to its built-in default, `localhost:5432`, which on a
machine with a native Postgres install is that server, not the container. It's the same
`postgres:17` image the integration tests start through Testcontainers, which use random ports, so
the two never clash.

⚠ **Gotcha — a native Postgres silently takes 5432.** With the Windows `postgresql-x64-*` service
running, a container published on `5432:5432` reports the port as bound, but `localhost:5432`
from Windows still reaches the native server. Migrations, `psql` and the API all succeed against
the wrong database. That's why the compose file uses 55432. Check with
`netstat -ano | findstr :5432`.

To create the schema in the container:

```pwsh
dotnet ef database update --project apps/api/src/EarlyEagles.Infrastructure --startup-project apps/api/src/EarlyEagles.Infrastructure
```

The Infrastructure project is its own startup project: it has the design-time
`ApplicationDbContextFactory` and the EF Design package, and the Api project doesn't.

**Check the result with psql (P0-033.4).** Any psql client works; the container's own is
`docker compose exec postgres psql -U postgres -d earlyeagles`:

```sql
\dt                                          -- tenants, tenant_members, audit_events, outbox_messages, __EFMigrationsHistory
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;   -- InitialCreate, AddAuditEvents, AddOutboxMessages
\d audit_events                              -- two triggers: audit_events_reject_update_delete / _reject_truncate
```

Running `dotnet ef database update` a second time prints *No migrations were applied*. To see the
append-only rule hold at the storage layer, try it inside a transaction you roll back. UPDATE,
DELETE and TRUNCATE on `audit_events` each fail with `audit_events is append-only: … is not
allowed`, and deleting a tenant that has audit rows fails on the `ON DELETE RESTRICT` foreign key.

⚠ **Gotcha — `ON DELETE CASCADE` stops at the audit log.** `tenant_members` cascades when a
tenant is deleted; `audit_events` is `RESTRICT`. Deleting a tenant with any audit history fails by
design. Tenant offboarding needs its own retention decision, not a `DELETE FROM tenants`.

**Service Bus emulator** (only for outbox / messaging work, P0-051). It sits behind the `bus` profile
because it needs a SQL Server container and ~2 GB of RAM:

```bash
cp .env.example .env                  # then set ACCEPT_EULA=Y and an MSSQL_SA_PASSWORD
docker compose --profile bus up -d
curl http://localhost:5300/health     # emulator management/health port
```

`ACCEPT_EULA=Y` is your acceptance of the emulator's and SQL Server's licence terms (links in
`.env.example`), so it isn't set for you. Connect from the host with
`Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`.
Entities are declared in [`infra/local/servicebus/Config.json`](../infra/local/servicebus/Config.json):
the `domain-events` topic (duplicate detection on, as in Bicep) and its `payments` subscription,
filtered to `Subject = PaymentConfirmed`. The list and the message contract are in §12.9 (P0-051).

⚠ **Gotcha — the emulator exits right after starting.** Usually `ACCEPT_EULA` isn't `Y`, or
`MSSQL_SA_PASSWORD` is empty or too weak. SQL Server needs 8+ characters with upper case, lower
case, a digit and a symbol, and the emulator can't reach a SQL container that never came up.
Check `docker compose --profile bus logs mssql`.

**Backend** (boots without a database — the DbContext is lazy; only `/ready` touches it):

```bash
dotnet run --project apps/api/src/EarlyEagles.Api --urls http://localhost:5080
```

```bash
curl http://localhost:5080/health   # {"status":"Healthy","version":"1.0.0+<sha>","uptimeSeconds":..}
curl http://localhost:5080/ready    # {"status":"Healthy","checks":[{"name":"postgres","status":"Healthy",...}],...}
                                    # 503 + "Database unreachable." while `docker compose stop postgres`
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

### 12.7 `containerAppsEnv.bicep` (P0-058.2)

Resource-group scoped, one per environment (`cae-ee-<env>-<region>`). It uses the serverless
**Consumption** workload profile (scale-to-zero, pay per use). The Container Apps from
P0-058.3 reference its `id` output as their `managedEnvironmentId`.

Logs go to the §12.4 workspace via the **`azure-monitor`** destination and a diagnostic
setting on the environment, not via the `log-analytics` destination:

```bicep
appLogsConfiguration: { destination: 'azure-monitor' }
...
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  properties: {
    workspaceId: workspaceId
    logs: [ { categoryGroup: 'allLogs', enabled: true } ]   // console + system logs
  }
}
```

`log-analytics` would need the workspace's shared key copied into the environment; this
route has no key to rotate or leak, and it's the only one that works behind Private Link.
Logs land in the `ContainerAppConsoleLogs` / `ContainerAppSystemLogs` tables. Metrics are not
copied into the workspace — they're already in Azure Monitor metrics, and copying them would
spend the daily cap.

Not included: Dapr (nothing in the stack uses it), VNet integration and zone redundancy
(zone redundancy requires a VNet; both are Phase 7 per conventions §9).

▶ Reference: [`infra/modules/containerAppsEnv.bicep`](../infra/modules/containerAppsEnv.bicep)

⚠ **Gotcha — the newest API version may have no Bicep types.** `Microsoft.App/managedEnvironments@2026-07-01`
is GA, but Bicep 0.47.16 ships no type definitions for it. You get `BCP081` ("does not have
types available") and — worse — **every property goes unchecked**, so a typo deploys and
fails (or is silently ignored) in Azure. Pin the newest version the CLI has types for
(`2026-01-01` here); a misspelt property then comes back as `BCP089` with a "did you mean".

⚠ **Gotcha — with `azure-monitor`, the environment's portal *Logs* blade is disabled.**
Query from the Log Analytics workspace instead (same tables). For real-time output use
`az containerapp logs show --follow`; ingested logs arrive several minutes late.

### 12.8 `containerApp.bicep` (P0-058.3)

Resource-group scoped, **one app per module call** — the API today, a worker or migrations
job later, each with its own role segment (`ca-ee-api-dev-eus-001`). Container App names cap
at **32 characters**, enforced with `@maxLength(32)`, so long role names fail the build rather
than the deploy.

Sizing is one input: `cpu` is one of the Consumption sizes (`'0.25'`…`'2'`, strings because
Bicep has no decimal type — converted with `json(cpu)`), and memory is looked up at the fixed
2 GiB-per-vCPU ratio, so an invalid CPU/memory pair can't be sent. `minReplicas` is 0 for
dev/staging (scale-to-zero) and 1 for prod (no cold start on the first request).

Probes are opt-in:

```bicep
module api 'modules/containerApp.bicep' = {
  scope: resourceGroup(rgName)
  params: {
    name: 'ca-ee-api-${environment}-eus-001', location: location, tags: tags
    environmentId: cae.outputs.id
    image: 'mcr.microsoft.com/k8se/quickstart:latest'   // placeholder, port 80
    targetPort: 80, externalIngress: true, cpu: '0.25', minReplicas: 0, maxReplicas: 1
    // livenessPath: '/health', readinessPath: '/ready'  ← once the real API image runs
  }
}
```

Identity is opt-in through one typed param, `identity: { id, clientId }?` (P0-062.1, see §12.18).
Not in the module yet: private-registry credentials and Key Vault secret references
(P0-062.3–.4). Until they land, the image must be publicly pullable and `env` holds only
non-secret values.

▶ Reference: [`infra/modules/containerApp.bicep`](../infra/modules/containerApp.bicep)

⚠ **Gotcha — a probe the image can't pass means the revision never becomes healthy.**
The placeholder image serves `/` on port 80 and nothing else. Setting `/health` or `/ready`
probes against it leaves the revision stuck unhealthy (and failing its traffic). Leave the
probe paths empty until the real API image is deployed, then set them in the same change.

### 12.9 `serviceBus.bicep` (P0-058.4)

Resource-group scoped. Creates the namespace (`sbns-ee-<env>-<region>-<unique>`, globally
unique like Key Vault) and whatever topics and subscriptions the caller passes as a typed
list — choosing them is P0-051's job:

```bicep
topics: [
  { name: 'domain-events', subscriptions: [ { name: 'payments' }, { name: 'audit-mirror', maxDeliveryCount: 5 } ] }
]
```

The module flattens that into one resource per subscription with a lambda
(`flatten(map(range(0, length(topics)), i => ...))`), keeping each subscription's parent
pointed at its own topic instance.

Fixed in the module:

- **`skuName` is `Standard` or `Premium`.** Basic has queues but no topics, and the outbox
  publishes to a topic — so Basic can't be passed at all.
- **`disableLocalAuth: true`.** No SAS keys, so no connection string exists. The API
  authenticates with Entra ID (`DefaultAzureCredential` → managed identity in Azure, your
  `az login` locally) and gets *Azure Service Bus Data Sender/Receiver* in P0-062.3.
- **Duplicate detection on every topic** (`duplicateDetectionWindow`, default `PT10M`).
  The outbox drain is at-least-once: if the process dies after publishing but before
  stamping the row, the next tick sends it again. The broker drops the repeat **if both
  sends carry the same `MessageId`**, so `ServiceBusOutboxPublisher` (P0-050.2.2) must set
  `MessageId` to the outbox row's id. Consumers must still be idempotent — a restart slower
  than the window gets past it.

The output `fullyQualifiedNamespace` (`<name>.servicebus.windows.net`) is what the Service Bus
client is constructed with.

▶ Reference: [`infra/modules/serviceBus.bicep`](../infra/modules/serviceBus.bicep)

⚠ **Gotcha — duplicate detection can't be added to an existing topic.** Service Bus
only accepts `requiresDuplicateDetection` when a topic is created; changing it later
means deleting and recreating the topic (and losing its messages and subscriptions). That's
why it's on from the first deploy even though nothing publishes yet.

**The topic list and the message contract (P0-051).** There is **one topic, `domain-events`**,
for every domain event, and one subscription per consumer. A subscription that names a
`subject` only receives messages whose `Subject` matches. Today that's one subscription,
`payments` (`subject: 'PaymentConfirmed'`) for the P0-052 handler. The same list is declared
twice and must stay in step:

| Where | Used by |
|---|---|
| [`infra/local/servicebus/Config.json`](../infra/local/servicebus/Config.json) | the local emulator (`docker compose --profile bus`) |
| `serviceBusTopics` default in [`main.dev.bicep`](../infra/main.dev.bicep) | sandbox deploys (P0-059.3); staging/prod pass theirs when they get Service Bus (P0-086/087) |

Every message the outbox publishes (P0-050.2.2) carries:

| Property | Value | Why |
|---|---|---|
| `MessageId` | outbox row id | duplicate detection drops re-sends |
| `Subject` | short event name, e.g. `PaymentConfirmed` | subscription routing (correlation filter) |
| `ApplicationProperties["EventType"]` | full CLR name | consumers that need the exact type |
| `ApplicationProperties["TenantId"]` | owning tenant | consumers re-establish tenant context |
| body / `ContentType` | the outbox payload / `application/json` | |

Routing on `Subject` rather than the full type name means moving an event class to another
namespace doesn't silently break a filter, and a correlation filter is cheaper for the broker
than a SQL one.

⚠ **Gotcha — a second rule filters nothing.** Every new subscription gets a `$Default` rule
that matches everything, and a subscription's rules are OR-ed. Adding a rule called `subject`
next to it would still deliver every message. The module writes the filter **as `$Default`**,
replacing the match-all.

⚠ **Gotcha — module paths must be relative.** A `module x 'D:/…/resourceGroup.bicep'`
reference fails with `BCP189: module reference scheme "D" is not recognized` — Bicep reads
the drive letter as a registry scheme (`br:`, `ts:`). Always reference modules relative to
the calling file.

### 12.10 `redis.bicep` (P0-058.5)

Resource-group scoped. **Azure Managed Redis** (`Microsoft.Cache/redisEnterprise`), not the
classic Azure Cache for Redis (`Microsoft.Cache/redis`) — the classic tiers stopped accepting new
instances on 2026-10-01 ([ADR-0008](./adr/0008-azure-managed-redis.md)). Two resources: the
cache (`amr-ee-<env>-<region>-<unique>`, a DNS name so it takes the §3.1 suffix) and its one
database, which must be named `default`.

```bicep
module redis 'modules/redis.bicep' = {
  name: 'redis'
  scope: resourceGroup(rgName)
  params: { name: 'amr-ee-dev-eus-${unique}', location: location, tags: tags,
            skuName: 'Balanced_B0', highAvailability: false }
}
```

`highAvailability` has no default on purpose — every environment file has to say whether it
wants a replica (off in dev/stg, on in prod per conventions §7).

Fixed in the module:

- **Access keys disabled** on the database. There is no password; the API connects with an Entra
  token (`Microsoft.Azure.StackExchangeRedis`). Access is granted with an **access-policy
  assignment** on the database (P0-062.3), not an Azure RBAC role.
- **TLS only, port 10000.** Not 6380 like the classic product.
- **No persistence.** Cache and locks only — durable state belongs in Postgres.
- **`clusteringPolicy` defaults to `NoCluster`**: one endpoint, so multi-key commands and Lua
  lock scripts behave like a plain Redis.

▶ Reference: [`infra/modules/redis.bicep`](../infra/modules/redis.bicep)

⚠ **Gotcha — clustering policy is (mostly) create-time.** `NoCluster` can later be switched to
`OSSCluster`/`EnterpriseCluster` in place; the reverse, or switching between the two clustered
policies, means deleting the database. That's why the default is the one that can still move.

⚠ **Gotcha — "Azure Cache for Redis" tutorials are now the wrong product.** Most samples still
show `Microsoft.Cache/redis` with `sku: { name: 'Basic', family: 'C', capacity: 0 }`. It builds
fine and fails at deploy with a creation-blocked error. Use `Microsoft.Cache/redisEnterprise`.

### 12.11 ~~`containerRegistry.bicep`~~ (P0-058.6) — removed

Built as one Basic Azure Container Registry shared by all environments, then **removed by
[ADR-0010](./adr/0010-ghcr-instead-of-acr.md)**: images now live on GitHub Container Registry
(`ghcr.io/razibur/early-eagles-api`), where private image storage and bandwidth are currently free.
CI pushes with its built-in `GITHUB_TOKEN`. The Container App pulls with a `read:packages`
token kept in Key Vault (P0-062.4); Container Apps can't use a managed identity against GHCR.
The module is recoverable from commit `208637a` if GitHub ever starts charging.

### 12.12 `main.dev.bicep` — the dev environment (P0-059.1)

> **Since [ADR-0009](./adr/0009-local-dev-no-cloud-dev-environment.md), dev runs locally** (`docker
> compose`, P0-059.5) and this template is deployed only as a throwaway sandbox smoke test.
> `EE_SANDBOX` now defaults to `true`; set it to `false` only to stand up a cloud dev on purpose.

The first file that *uses* the modules. Subscription-scoped: it creates `rg-ee-dev-eus` and
deploys every per-environment module into it.

```
resourceGroup ─┬─ logAnalytics ─┬─ appInsights
               │                └─ containerAppsEnv ── containerApp (api)
               ├─ keyVault
               ├─ postgresFlexible
               ├─ serviceBus
               └─ redis
```

What goes where:

- **In the file:** what makes it *dev*. The env code, region, every resource name (§3) and the
  §7 dev sizes (Postgres B1ms Burstable, Redis B0 without HA, Container App scaling 0→1, …).
- **In the parameter file (P0-059.2):** what's specific to *this* deployment. Tag `owner`/
  `costCenter`, the Postgres Entra admin, operator IPs for `psql`, and the Log Analytics daily cap,
  which is required and has no default.

Module deployments are named `<top-level deployment name>-<module>` (since P0-060.1 with an `-env` level in between; e.g.
`ee-dev-20261006-1430-env-psql`), so the per-run history §6 asks for is kept at every level.

Deliberately not wired yet: pulling the private image from ghcr.io (P0-062.4, [ADR-0010](./adr/0010-ghcr-instead-of-acr.md)), role assignments for the API identity (P0-062.2–.4; the identity itself landed in P0-062.1, §12.18), app env vars and health probes
(with the real API image, P0-084), and Service Bus topics (P0-051 passes `serviceBusTopics`).

▶ Reference: [`infra/main.dev.bicep`](../infra/main.dev.bicep)

⚠ **Gotcha — a sandbox deploy can squat the real Key Vault name.** Purge protection is on
everywhere, so a deleted vault's name stays reserved for its whole soft-delete retention (90 days).
If a throwaway deploy used `kv-ee-dev-eus-<suffix>`, tearing it down would block the real dev vault
for three months. `sandbox = true` deploys to `rg-ee-sandbox-eus`, seeds the suffix with
`dev-sandbox` (new Key Vault/Service Bus/Redis names), adds `-sbx` to the Postgres name and sets the
vault's retention to 7 days.

⚠ **Gotcha — `scope: resourceGroup(rg.outputs.name)` doesn't compile (`BCP120`).** A module's scope has to be
known when the deployment starts, so it can't come from another module's output. Scope by the
`resourceGroupName` variable and add `dependsOn: [rg]` instead. Modules that already consume an
output (App Insights, the Container Apps env, the app) get the dependency implicitly.

### 12.13 `main.dev.bicepparam` — dev values (P0-059.2)

Shared dev values are committed (`owner`, `costCenter`, a **1 GB/day** log cap). Values tied to a
person or machine come from environment variables, so set them in the shell before building or
deploying:

```pwsh
az login                                   # into the project tenant/subscription
$env:EE_PG_ADMIN_OBJECT_ID = az ad signed-in-user show --query id -o tsv
$env:EE_PG_ADMIN_NAME      = az ad signed-in-user show --query userPrincipalName -o tsv
$env:EE_OPERATOR_IP        = (Invoke-RestMethod https://api.ipify.org)   # optional: lets you psql in
# $env:EE_SANDBOX          = 'false'                                      # default is 'true' (ADR-0009)

az bicep build-params --file infra/main.dev.bicepparam   # sanity check
```

The two `EE_PG_ADMIN_*` variables have no default: if either is missing, Bicep stops with `BCP427`
before anything is sent to Azure. The operator IP is optional. Without it Postgres has no
firewall rules, which is fine until you want to `psql` in from your machine.

Three more optional variables, in every environment's param file (added with P0-063), cover the
values that change per deploy rather than per person:

| Variable | Default | For |
|---|---|---|
| `EE_API_IMAGE` | the public placeholder | the real image, e.g. `ghcr.io/razibur/early-eagles-api:<tag>` (CI sets it, P0-084) |
| `EE_API_PORT` | `80` | the port that image listens on |
| `EE_PG_ALLOW_AZURE_SERVICES` | `false` | opens Postgres to Azure-hosted services once the API really connects |

They're environment variables rather than edits to the committed files, so a deploy never
leaves a diff behind. The full order of operations, from first deploy to the real image, is the
[deploy-infra runbook](./runbooks/deploy-infra.md).

▶ Reference: [`infra/main.dev.bicepparam`](../infra/main.dev.bicepparam)

⚠ **Gotcha — a guest account's UPN isn't its email.** A Microsoft account (e.g. an outlook.com
address) invited into a tenant has a UPN like `name_outlook.com#EXT#@tenant.onmicrosoft.com`. Use
the value `az ad signed-in-user show` returns, not the email address. It's also the user name
`psql` expects.

### 12.14 ~~`main.shared.bicep`~~ (P0-059.4) — removed

Deployed the shared registry's resource group (`rg-ee-shared-eus`). Removed together with the
registry by [ADR-0010](./adr/0010-ghcr-instead-of-acr.md), along with the `shared` environment
code in `types.bicep`. Recoverable from commit `8cf194d`.

### 12.15 `environment.bicep` — one wiring for every environment (P0-060.1)

Once a second environment was coming, `main.dev.bicep`'s body moved into
[`infra/environment.bicep`](../infra/environment.bicep): the module graph from §12.12, every
resource name, the tag object and the sandbox logic. Each `main.<env>.bicep` is now a thin caller
that fixes two things:

- `environment`: `'dev'`, `'stg'` or `'prod'`.
- `sizing`: a typed `environmentSizing` object holding the §7 column for that environment.

```bicep
import { environmentSizing } from 'environment.bicep'

var sizing environmentSizing = {
  logRetentionInDays: 30
  postgres: { skuName: 'Standard_B1ms', skuTier: 'Burstable', storageSizeGB: 32,
              backupRetentionDays: 7, highAvailabilityMode: 'Disabled', geoRedundantBackup: 'Disabled' }
  redis:    { skuName: 'Balanced_B0', highAvailability: false }
  api:      { cpu: '0.25', minReplicas: 0, maxReplicas: 1 }
}
```

The environments can now differ only in the values they pass, never in how resources are wired.
The parameter files are untouched: `main.dev.bicep` kept exactly the same parameters.

How the refactor was checked: compile `main.dev.bicep` before and after, then compare each
module's inner template (identical) and parameters (equal once the `sizing` references are
resolved). Nested deployment names gain one level, e.g. `ee-dev-20261006-1430-env-psql`.

▶ Reference: [`infra/environment.bicep`](../infra/environment.bicep)

### 12.16 `main.staging.bicep` (P0-060.2)

The second caller of `environment.bicep`: `environment: 'stg'` and the §7 stg sizing, which today
matches dev. Staging is production-shaped in **topology** (same modules, same wiring, its own
resource group `rg-ee-stg-eus`), not in size. Resize it here if a load test ever needs it.

It takes the same parameters as dev except `sandbox`, which is hard-wired off: there's one shared
`rg-ee-sandbox-eus`, and throwaway deploys go through `main.dev.bicep`.
`main.staging.bicepparam` mirrors the dev file, with the same `EE_PG_ADMIN_*` /
`EE_OPERATOR_IP` variables (§12.13).

```pwsh
az deployment sub create --name ee-stg-$(Get-Date -Format yyyyMMdd-HHmm) --location eastus `
  --template-file infra/main.staging.bicep --parameters infra/main.staging.bicepparam
```

▶ Reference: [`infra/main.staging.bicep`](../infra/main.staging.bicep)

### 12.17 `main.prod.bicep` — and the minimum-cost posture (P0-061)

The owner's call for this platform is **as close to free as possible** (conventions §7, cost
posture). Prod therefore uses the dev sizes: Burstable `B1ms` Postgres with **no HA**, an API that
**scales to zero** (max 2 replicas, so the first request after idle has a few seconds of cold
start), 30-day logs and a 1 GB/day cap.

To make that possible, `environment.bicep` treats **Redis and Service Bus as optional**: they're
deployed only when the environment's `sizing` includes them.

```bicep
var sizing environmentSizing = {
  // …postgres, api, logRetentionInDays…
  serviceBus: { skuName: 'Standard' }     // present → deployed; leave out → not deployed
  // redis: { skuName: 'Balanced_B0', highAvailability: false }
}
```

Today **no environment has Redis** (nothing uses it), and **only dev has Service Bus** (the outbox
work, P0-051, needs it). When a resource is off, its outputs come back empty (`''` / `0`).
Turning one on is adding the block back, not a module change.

```pwsh
az deployment sub create --name ee-prod-$(Get-Date -Format yyyyMMdd-HHmm) --location eastus `
  --template-file infra/main.prod.bicep --parameters infra/main.prod.bicepparam
```

▶ Reference: [`infra/main.prod.bicep`](../infra/main.prod.bicep)

⚠ **Gotcha — reading an output of a conditional module.** `redis.outputs.hostName` fails the
build with `BCP318` ("may be null at the start of the deployment") when the module has an `if`. Use the safe-access form
`redis.?outputs.?hostName ?? ''`, which compiles to `tryGet(...)` + `coalesce(...)`.

### 12.18 `userAssignedIdentity.bicep` — the API's managed identity (P0-062.1)

The API authenticates to Azure (Postgres, Key Vault, Service Bus) as a **managed identity**, so
no password or connection-string secret exists for those services. `environment.bicep` creates
one per environment, `id-ee-api-<env>-eus` (CAF abbreviation `id`, conventions §4), and attaches
it to the Container App:

```bicep
module apiIdentity 'modules/userAssignedIdentity.bicep' = {
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: { name: 'id-ee-api-${environment}-eus', location: location, tags: tags }
}

module api 'modules/containerApp.bicep' = {
  params: {
    // …
    identity: { id: apiIdentity.outputs.id, clientId: apiIdentity.outputs.clientId }
  }
}
```

**User-assigned rather than system-assigned.** A system-assigned identity only exists once the
app does. The app's first revision has to pull its private image with a token that the identity
reads from Key Vault (P0-062.4), so the identity and its access have to come first. A
user-assigned identity also keeps its role assignments if the app is deleted and recreated.
There's one identity per role (api now, later worker / migrations), so each gets only the access
it needs.

The module outputs `id`, `principalId` (the object that role assignments in P0-062.2/.3 grant
to) and `clientId`. The Container App sets **`AZURE_CLIENT_ID`** to the client ID, so
`DefaultAzureCredential` in the API picks this identity without guessing. The entry points
re-export `apiIdentityPrincipalId` and `apiIdentityClientId`.

`identity` is a nullable typed object, not two loose strings, so an id without its client ID is a
build error (`BCP035`). Leaving it out compiles to `identity: { type: 'None' }`.

▶ Reference: [`infra/modules/userAssignedIdentity.bicep`](../infra/modules/userAssignedIdentity.bicep)

⚠ **Gotcha — `DefaultAzureCredential` with a user-assigned identity needs `AZURE_CLIENT_ID`.**
Without it, the managed-identity credential asks IMDS for *the* identity and gets an error when
the identity isn't system-assigned (or when more than one is attached). The module sets it
whenever an identity is passed; don't remove it from `env`.

### 12.19 The identity's Postgres role — `infra/sql/api-role/` (P0-062.2.2)

Bicep can create the identity and the server, but **not a role inside Postgres**. That's
data-plane SQL, and ARM doesn't run it. Two scripts, run once per environment by the server's
Entra administrator after deploy:

| Script | Database | Does |
|---|---|---|
| [`01-create-principal.sql`](../infra/sql/api-role/01-create-principal.sql) | `postgres` | `pgaadauth_create_principal_with_oid(name, principalId, 'service', false, false)`: a role named after the identity, bound to its object ID. Azure only. Skips if the role exists. |
| [`02-grants.sql`](../infra/sql/api-role/02-grants.sql) | `earlyeagles` | CONNECT, schema USAGE, SELECT/INSERT/UPDATE/DELETE on all tables, and the same as **default privileges** for tables future migrations create |

```pwsh
psql "host=<server> dbname=postgres user=<entra-admin> sslmode=require" `
  -v role_name=id-ee-api-dev-eus -v principal_id=<apiIdentityPrincipalId> `
  -f infra/sql/api-role/01-create-principal.sql
psql "host=<server> dbname=earlyeagles user=<entra-admin> sslmode=require" `
  -v role_name=id-ee-api-dev-eus -f infra/sql/api-role/02-grants.sql
```

**No DDL.** The API never calls `Migrate`; migrations run as the admin. So the API role can't
`CREATE`, `ALTER`, `DROP` or `TRUNCATE`, and it isn't allowed to read `__EFMigrationsHistory`.
`audit_events` gets **SELECT + INSERT only**. Its trigger already rejects UPDATE/DELETE, and
withholding the privileges too means dropping the trigger alone doesn't reopen the table.

Both scripts are idempotent, and they fail with a non-zero exit when a `-v` variable is missing
(`RAISE EXCEPTION` under `ON_ERROR_STOP`, because psql's `\quit` exits 0).

**Testing locally** (no `pgaadauth` there): copy the migrated database
(`CREATE DATABASE x TEMPLATE earlyeagles`), create a stand-in `CREATE ROLE … LOGIN PASSWORD …`,
then run `02-grants.sql` against the copy.

⚠ **Gotcha — default privileges are per creator.** `ALTER DEFAULT PRIVILEGES` only applies to
tables created by the role it names (`FOR ROLE`). The script uses the role running it unless you
pass `-v migration_owner=<role>`. If migrations ever run as a different role (a migrations
identity, say), re-run the script with that role. Otherwise new tables arrive with no grants,
and the API gets `permission denied` on its first query against them.

⚠ **Gotcha — `principal_id` is the object ID, not the client ID.** Use the
`apiIdentityPrincipalId` output. `AZURE_CLIENT_ID` is the other one.

**Wiring the app (P0-062.2.3).** `environment.bicep` gives the API two plain env vars (nothing in
them is secret):

```bicep
env: [
  { name: 'Postgres__Authentication', value: 'Entra' }
  { name: 'ConnectionStrings__Postgres'
    value: 'Host=${psql.outputs.fqdn};Database=${psql.outputs.databaseName};Username=${apiIdentity.outputs.name};SslMode=VerifyFull' }
]
```

`Username` is the identity's name, taken from the module output rather than retyped, and the
entry points export it as `apiIdentityName`. Pass that output as `role_name` to the SQL scripts,
so the role and the connection string always agree. There's no `Password`. The API fetches a
token instead (§9, P0-062.2.1), and refuses to start in Entra mode if a password is present.

⚠ **Gotcha — Npgsql's `SslMode=Require` doesn't check the certificate.** It encrypts, but it
accepts any server certificate. `VerifyFull` also checks the chain and the host name. Azure
Postgres certificates chain to public roots in the .NET container image's trust store, so it
needs no extra setup.

### 12.20 The identity's access to Key Vault, Service Bus and Redis (P0-062.3)

Every data service here has its keys or passwords turned off (Key Vault RBAC, Service Bus
`disableLocalAuth`, Redis `accessKeysAuthentication: 'Disabled'`), so an identity can only get
in through an explicit grant. Each grant lives **in the module that owns the resource**, as a
list of principal IDs, and `environment.bicep` passes the API identity:

| Module | Param | Grant | Scope |
|---|---|---|---|
| `keyVault.bicep` | `secretsUserPrincipalIds` | RBAC **Key Vault Secrets User** (read secrets only) | the vault |
| `serviceBus.bicep` | `dataSenderPrincipalIds` / `dataReceiverPrincipalIds` | RBAC **Azure Service Bus Data Sender / Receiver** | the namespace |
| `redis.bicep` | `dataAccessPrincipalIds` | Redis **access-policy assignment** to `default` (not RBAC) | the database |

Putting the grant inside the module means it's deployed exactly when the resource is. Service
Bus exists only in dev today, and Redis nowhere, so their grants follow automatically. It also
needs no `existing` lookups. The role assignment itself:

```bicep
resource secretsUsers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in secretsUserPrincipalIds: {
    name: guid(vault.id, principalId, keyVaultSecretsUserRoleId)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]
```

The API gets both Service Bus roles because it both publishes (the outbox drain) and consumes
(the P0-052 handlers). When a separate worker exists, the receiver role moves to its identity.

⚠ **Gotcha — set `principalType` on role assignments for a new identity.** Without it, ARM
looks the principal up in Entra ID. An identity created seconds earlier in the same deployment
often hasn't replicated yet, and the assignment fails with `PrincipalNotFound`. With
`ServicePrincipal` set, the lookup is skipped.

⚠ **Gotcha — role assignment names must be GUIDs, and stable.** Use
`guid(scope, principal, role)`. A random or changing name makes a redeploy try to create a
second assignment of the same role, which fails with `RoleAssignmentExists`.

⚠ **Gotcha — verify built-in role IDs; don't trust memory.** They're the same in every tenant,
but they're 36 hex characters. My remembered Service Bus Data Receiver ID was off by one
character, and nothing in Bicep would have caught that before a failed deploy. The source of
truth is Microsoft's built-in roles reference, or `az role definition list --name "<role>"`.

⚠ **Gotcha — Redis data access isn't RBAC.** Azure RBAC on `redisEnterprise` covers the control
plane only. Data access is a `databases/accessPolicyAssignments` child. The client signs in with
the identity's **object ID as the user name** and an Entra token as the password.

### 12.21 Pulling the private `ghcr.io` image (P0-062.4)

Images live on GitHub Container Registry ([ADR-0010](./adr/0010-ghcr-instead-of-acr.md)), and
they're private. Container Apps can use a managed identity only with ACR, so for GHCR the app
pulls with a **GitHub token scoped to `read:packages` only**. The token sits in Key Vault, and
the app reads it **as its managed identity**:

```bicep
// containerApp.bicep (with `registry` set)
secrets: [
  { name: 'registry-password', keyVaultUrl: registry.passwordKeyVaultSecretUrl, identity: identity.id }
]
registries: [
  { server: 'ghcr.io', username: 'razibur', passwordSecretRef: 'registry-password' }
]
```

`environment.bicep` attaches the registry **only when `apiImage` starts with `ghcr.io/`**. The
public placeholder image needs none, and referencing a secret that doesn't exist yet would fail
the deploy. Because the secret URL comes from `kv.outputs.uri`, the app deploys after the vault,
and so after the identity's Secrets User grant (§12.20).

**Before the first `ghcr.io` deploy of an environment** (operator, once):

1. Create a GitHub **classic** personal access token with **only `read:packages`**, and give it
   an expiry. GitHub Packages doesn't accept fine-grained tokens.
2. Give yourself permission to write secrets. The vault uses RBAC, and Owner/Contributor
   include **no data access**:
   ```pwsh
   $kv = az keyvault show -g rg-ee-dev-eus -n <vault> --query id -o tsv
   az role assignment create --role "Key Vault Secrets Officer" --scope $kv `
     --assignee (az ad signed-in-user show --query id -o tsv)
   ```
3. Store the token. Read it from a file so it doesn't land in your shell history, and record the
   expiry so it shows up in the portal:
   ```pwsh
   az keyvault secret set --vault-name <vault> --name ghcr-pull-token `
     --file token.txt --expires 2027-10-26T00:00:00Z
   Remove-Item token.txt
   ```
4. Deploy with `apiImage = 'ghcr.io/razibur/early-eagles-api:<tag>'`.

**Rotating** means `az keyvault secret set` with a new token. The app's reference has **no
version**, so Container Apps picks up the new value (it re-reads Key Vault references
periodically, and a new revision always reads the latest). No redeploy is needed. Rotation goes
in the infra runbook (P0-063).

⚠ **Gotcha — a registry without an identity can't read the password.** The module calls
`fail('… registry requires identity …')` in that case, inside `identity ?? fail(…)`, so the
deployment fails validation with that message. `??` short-circuits, so `fail` only runs when the
identity really is missing.

⚠ **Gotcha — first deploy right after the grant can still fail the pull.** An RBAC assignment can
take a few minutes to take effect. If the first revision reports it can't read the Key Vault
secret, wait and redeploy; nothing needs changing.

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
| **SignalR / Redis / Service Bus** | later phases | realtime, cache/locks (Azure Managed Redis, [ADR-0008](./adr/0008-azure-managed-redis.md)), messaging |
| **Cloud + IaC** | conventions written | **Azure** + **Bicep** (`infra/`), per [ADR-0002](./adr/0002-enterprise-saas-reframe.md). Naming, tagging, region and SKU rules are settled in [`infra-conventions.md`](./infra-conventions.md); the P0-057 modules (resource group, Key Vault, Log Analytics, App Insights), the P0-058.1–.5 modules (Postgres, Container Apps environment, Container App, Service Bus, Redis; the registry was dropped for ghcr.io per ADR-0010) and the shared tag type exist (§12), every planned module, the shared `environment.bicep` wiring, the dev, staging and prod entry points with their parameter files exist (§12.12–12.17), sized for minimum cost, the API has a user-assigned managed identity (§12.18; role assignments P0-062.2–.4 next), and nothing is deployed. |
| **CI/CD** | not started | GitHub Actions: build + push on `main` merge, staging on demand, manual prod promote ([ADR-0009](./adr/0009-local-dev-no-cloud-dev-environment.md) — dev runs locally) |

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
| Azure.Identity | 1.21.0 |
| Azure.Messaging.ServiceBus | 7.21.0 |
| Microsoft.Extensions.Hosting.Abstractions / Options.ConfigurationExtensions | 10.0.11 |
| Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 10.0.11 |
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
| `Microsoft.App/managedEnvironments` API | 2026-01-01 (newest with Bicep types) |
| `Microsoft.Insights/diagnosticSettings` API | 2021-05-01-preview |
| `Microsoft.App/containerApps` API | 2026-01-01 |
| `Microsoft.ServiceBus/namespaces` (+ topics, subscriptions) API | 2024-01-01 |
| `Microsoft.ManagedIdentity/userAssignedIdentities` API | 2024-11-30 |

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
| `BCP081: does not have types available` | API version newer than the Bicep CLI's type library; properties go unchecked | pin the newest version the CLI has types for |
| Container Apps environment has no *Logs* blade in the portal | logs destination is `azure-monitor` | query the Log Analytics workspace directly |
| Users keep running an old service worker after a deploy | `/sw.js` came from a cache | keep `updateViaCache: "none"` + `no-cache` on `/sw.js` (§4.1) |
| `npm run build`: `'textTransform' does not exist in type …XAxisTickContentProps` | CSS-only property passed as an SVG attribute in a Recharts `tick` object | move it into `tick.style` (§4.1) |
| DevTools says the app isn't installable: *incognito* | private / automation profile | test in a normal profile (§4.2) |
| `MissingMessageHandlerException` though the handler exists | `options.Assemblies` set from a variable; the generator scanned nothing | use `[typeof(AssemblyMarker)]` (§9.8) |
| `FindAsync` returns null for a row that exists | tenant query filter, no tenant resolved | `IgnoreQueryFilters()` for setup code (§9.8) |
| `/ready` hangs ~15 s when the DB host is unresponsive | Npgsql connect timeout, no check timeout | register the check with `timeout:` (§7) |
| New Container App revision never becomes healthy | liveness/readiness probe on a path the image doesn't serve | leave `livenessPath`/`readinessPath` empty for placeholder images |
| Service Bus rejects a connection string / SAS token (auth failure) | namespace has SAS keys disabled | authenticate with Entra (`DefaultAzureCredential`) + a data role |
| Redis deploy fails: creation blocked / SKU unavailable | `Microsoft.Cache/redis` (Azure Cache for Redis) — closed to new instances since 2026-10-01 | use `Microsoft.Cache/redisEnterprise` (Azure Managed Redis), ADR-0008 |
| Redis client can't connect on 6380 / rejects the access key | Azure Managed Redis listens on **10000** and has access keys disabled | port 10000 + Entra token via `Microsoft.Azure.StackExchangeRedis` |
| Real dev deploy fails: Key Vault name already in use, but nothing is in `rg-ee-dev-eus` | an earlier throwaway deploy used the real name; the deleted vault is purge-protected | deploy throwaways with `sandbox = true`; otherwise wait out retention or recover the vault |
| `BCP427: Environment variable "EE_PG_ADMIN_OBJECT_ID" does not exist` | `main.dev.bicepparam` reads the Postgres admin from the shell | set `EE_PG_ADMIN_OBJECT_ID` / `EE_PG_ADMIN_NAME` (§12.13) |
| `BCP318: The value of type "module | null" may be null` | reading an output of a module that has an `if` | use `mod.?outputs.?x ?? <fallback>` |
| API in Azure gets `ManagedIdentityCredential authentication unavailable` / no identity found | user-assigned identity attached but `AZURE_CLIENT_ID` not set | pass `identity` to `containerApp.bicep` — it sets the variable (§12.18) |
| Container App can't pull the `ghcr.io` image (`UNAUTHORIZED`) | image is private; the app has no registry credentials, or the `read:packages` token expired | wire / rotate the token (P0-062.4, [ADR-0010](./adr/0010-ghcr-instead-of-acr.md)) |
| Migrations / `psql` "succeed" but the container DB is empty | a native Windows Postgres owns `localhost:5432` and took the connection | use the compose port 55432 + `ConnectionStrings__Postgres` (§11) |
| `dotnet ef`: startup project doesn't reference `Microsoft.EntityFrameworkCore.Design` | Api project used as startup | `--startup-project apps/api/src/EarlyEagles.Infrastructure` |
| Service Bus emulator container exits immediately | `ACCEPT_EULA` not `Y`, or `MSSQL_SA_PASSWORD` empty/weak, so SQL Server never starts | set both in `.env` (§11); `docker compose --profile bus logs mssql` |
| Background poller reads zero rows forever | its scope has no ambient tenant, so the query filter matches nothing | `IgnoreQueryFilters()` on the drain query |

---

*Keep this document current: when a build step, version, or setup gotcha changes, update the
relevant section and Appendix A/B in the same change. See [PROGRESS.md](../PROGRESS.md) for
live task status.*

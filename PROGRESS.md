# PROGRESS

Live dashboard for the EarlyEagles build. Scan this in 30 seconds to see what's
done, what's in flight, and what's next. The authoritative task list with full
descriptions lives in [Implementation-Plan.md](./Implementation-Plan.md); this
file mirrors it with status + verification.

**Legend**

- `[ ]` pending
- `[~]` in progress
- `[x]` done
- `[-]` deferred (see linked ADR)

Each task carries a one-line **verify:** with the exact thing to check / command
to run. If a verify step requires external accounts (Azure, Stripe, Clerk),
that's noted — the rest you can verify on your laptop.

Last updated: 2026-06-18

---

## Phase 0 — Enterprise Foundations  ▰▱▱▱▱▱▱▱▱▱  ~21% (39 / ~190 tasks)

The non-negotiable platform foundation. **P0-034.* (tenant-isolation integration test) is the gate** — every isolation sub-task must stay green before any feature beyond Phase 0 ships.

Re-granulated 2026-05-25 per the [small-task rule](file:///c:/Users/skyli/.claude/projects/d--Projects-EarlyEagles/memory/feedback_small_tasks.md): each task 1–3 hours, single concept, individually verifiable. Sub-IDs (`.1`, `.2`, …) decompose bundles in the previous coarser plan.

### Pre-work — accounts, domain, secrets

External account / billing tasks. Mostly off the laptop; you confirm by logging into each console.

- `[ ]` **P0-001** Register `earlyeagles.club` via Cloudflare Registrar
  ↳ verify: `whois earlyeagles.club` shows ownership; auto-renew on
- `[ ]` **P0-002** Create Cloudflare account + add domain DNS zone
  ↳ verify: Cloudflare manages the zone; NS records resolve
- `[ ]` **P0-003** Create Azure subscription + $50/mo dev billing alert
  ↳ verify: test alert email arrives when threshold simulated
- `[ ]` **P0-004** Create GitHub org + private repo `early-eagles-platform`
  ↳ verify: repo URL resolves; you're owner
- `[ ]` **P0-005** Set up password-manager vault for project secrets
  ↳ verify: vault entry exists, can be unlocked
- `[ ]` **P0-006** Create Clerk account + production application
  ↳ verify: dashboard shows publishable + secret keys
- `[ ]` **P0-007** Create Stripe account (test mode) + verify business
  ↳ verify: dashboard accessible, test mode enabled
- `[ ]` **P0-008** Create Sentry account + two projects (backend + frontend)
  ↳ verify: two DSNs in vault
- `[ ]` **P0-009** Create Better Stack account + status page placeholder
  ↳ verify: status page URL resolves
- `[ ]` **P0-010** Create PostHog account + project
  ↳ verify: API key in vault
- `[ ]` **P0-011** Create Resend account + verify sending domain
  ↳ verify: DKIM/SPF passing; test email arrives
- `[ ]` **P0-012** Create Flagsmith account + project + dev environment
  ↳ verify: dev environment shows in dashboard
- `[ ]` **P0-013** Create Cloudflare R2 bucket `ee-storage-dev`
  ↳ verify: `wrangler r2 bucket list` shows bucket

### Repository scaffolding

- `[x]` **P0-014.1** Create `README.md` with tagline + tech overview + quick start
  ↳ verify: renders on GitHub repo home
- `[x]` **P0-014.2** Add `LICENSE` (MIT or Apache-2.0)
  ↳ verify: visible in GitHub repo header
- `[x]` **P0-014.3** Add `.gitignore` for .NET / Node / Bicep / OS / secrets
  ↳ verify: build artifacts don't show in `git status`
- `[x]` **P0-014.4** Add `.editorconfig` (indentation, line endings, final newline)
  ↳ verify: VS / Rider / VSCode all respect settings
- `[ ]` **P0-014.5** Add `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1)
  ↳ verify: file present, linked from README
- `[ ]` **P0-014.6** Add `CONTRIBUTING.md` (branch + commit + PR conventions)
  ↳ verify: file present, linked from README
- `[ ]` **P0-015** Branch protection on `main` (PR review, status checks, signed commits)
  ↳ verify: direct push to `main` blocked
- `[ ]` **P0-016** Add `CODEOWNERS` file
  ↳ verify: test PR auto-requests you as reviewer
- `[ ]` **P0-017** Add `docs/adr/0000-template.md` ADR template
  ↳ verify: file exists with template scaffold
- `[x]` **P0-018** Move existing planning docs into repo
  ↳ verify: all cross-references resolve
- `[x]` **P0-019** Monorepo layout (`apps/web`, `apps/api`, `infra`, `docs`) + workspace config
  ↳ verify: `npm install` from root installs all workspaces

### Backend — Clean Architecture solution

Solution scaffolded under [`apps/api/`](./apps/api). **Targets .NET 10 (LTS), not .NET 9** — .NET 9 STS support ended May 2026; bumping the target framework is the only deviation from the plan, all other picks (Clean Architecture, EF Core, NetArchTest) stand.

- `[x]` **P0-020.1** Create empty `apps/api/EarlyEagles.slnx`
  ↳ verify: `dotnet sln list` returns the 7 projects
- `[x]` **P0-020.2** Add `EarlyEagles.Domain` class library
- `[x]` **P0-020.3** Add `EarlyEagles.Application`, reference Domain
- `[x]` **P0-020.4** Add `EarlyEagles.Infrastructure`, reference Application
- `[x]` **P0-020.5** Add `EarlyEagles.Api` (ASP.NET Core 10), reference Application + Infrastructure
- `[x]` **P0-020.6** Add `EarlyEagles.Tests.Unit` xUnit project
- `[x]` **P0-020.7** Add `EarlyEagles.Tests.Integration` xUnit project
- `[x]` **P0-020.8** Add `EarlyEagles.Tests.Acceptance` xUnit project
- `[x]` **P0-020.9** Architecture test (NetArchTest) enforcing Domain ⊥ Application + Infrastructure, Application ⊥ Infrastructure
  ↳ verify: `dotnet test tests/EarlyEagles.Tests.Unit` — 3 passed
- `[x]` **P0-020.10** Whole-solution `dotnet build` returns 0 errors + 0 warnings
- `[x]` **P0-021.1** `Entity<TId>` base class with Id + equality
  ↳ verify: [`Entity.cs`](./apps/api/src/EarlyEagles.Domain/Primitives/Entity.cs) — type+Id equality; 5 EntityTests pass
- `[x]` **P0-021.2** `ValueObject` base class with structural equality
  ↳ verify: [`ValueObject.cs`](./apps/api/src/EarlyEagles.Domain/Primitives/ValueObject.cs) — `GetEqualityComponents()` SequenceEqual; 5 ValueObjectTests pass
- `[x]` **P0-021.3** `AggregateRoot` base class with domain-event collection
  ↳ verify: [`AggregateRoot.cs`](./apps/api/src/EarlyEagles.Domain/Primitives/AggregateRoot.cs) — Raise / Clear / read-only `DomainEvents`; 4 AggregateRootTests pass
- `[x]` **P0-021.4** `IDomainEvent` interface (dispatch contract lands in P0-049)
  ↳ verify: [`IDomainEvent.cs`](./apps/api/src/EarlyEagles.Domain/Primitives/IDomainEvent.cs) — `Id` + `OccurredAt`; no MediatR dep
- `[x]` **P0-021.5** Unit tests for Entity / ValueObject / AggregateRoot semantics
  ↳ verify: `dotnet test tests/EarlyEagles.Tests.Unit` — 17 passed (3 arch + 14 primitives)
- `[x]` **P0-022.1** Add StyleCop + Roslyn analyzers via `Directory.Build.props`
  ↳ verify: [`apps/api/Directory.Build.props`](./apps/api/Directory.Build.props) centralizes TFM/Nullable/AnalysisLevel + StyleCop package; thin per-project csprojs
- `[x]` **P0-022.2** Configure StyleCop ruleset; turn warnings into errors
  ↳ verify: [`apps/api/.editorconfig`](./apps/api/.editorconfig) sets severities + `TreatWarningsAsErrors=true`; `dotnet build` clean (0/0)
- `[x]` **P0-023.1** Install `martinothamar/Mediator` (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md), replaces MediatR); `AddMediator` scans Application assembly
  ↳ verify: [`DependencyInjection.cs`](./apps/api/src/EarlyEagles.Application/DependencyInjection.cs) wires `AddApplication`; `dotnet build` clean
- `[x]` **P0-023.2** Install FluentValidation; register validators from Application assembly
  ↳ verify: `AddValidatorsFromAssembly(applicationAssembly, ServiceLifetime.Scoped)` in `DependencyInjection.cs`
- `[x]` **P0-023.3** Install `Riok.Mapperly` (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md), replaces AutoMapper); no profile registration needed — source-generated mappers register themselves when added
  ↳ verify: `Riok.Mapperly` package referenced as PrivateAssets in [`EarlyEagles.Application.csproj`](./apps/api/src/EarlyEagles.Application/EarlyEagles.Application.csproj)
- `[x]` **P0-024.1** `LoggingBehavior` Mediator pipeline (with correlation ID)
  ↳ verify: [`LoggingBehavior.cs`](./apps/api/src/EarlyEagles.Application/Behaviors/LoggingBehavior.cs) — enter / exit / duration logs via `LoggerMessage` source-gen; `Activity.Current?.TraceId` as correlation ID in `BeginScope`; 3 LoggingBehaviorTests pass
- `[x]` **P0-024.2** `ValidationBehavior` Mediator pipeline (runs FluentValidation)
  ↳ verify: [`ValidationBehavior.cs`](./apps/api/src/EarlyEagles.Application/Behaviors/ValidationBehavior.cs) — runs all `IValidator<TMessage>` in parallel, throws `ValidationException` on any failure; 5 ValidationBehaviorTests pass
- `[~]` **P0-024.3** `TransactionBehavior` Mediator pipeline (wraps writes in TX)
  ↳ behavior + `IUnitOfWork` / `IUnitOfWorkTransaction` abstractions written + 4 unit tests pass (Begin→Commit on success, Begin→Dispose-without-Commit on exception, cancellation token propagation, null-next). **Pipeline registration deferred to P0-032.2** when the EF Core concrete implementation lands. Reopen this checkbox when wired into `AddApplication.PipelineBehaviors`.
- `[~]` **P0-024.4** `AuditBehavior` Mediator pipeline (writes AuditEvent — wires to P0-047)
  ↳ behavior + `IAuditService` / `AuditEntry` abstractions written; gates on `Mediator.ICommand` / `ICommand<TResponse>` so queries pass through; 5 unit tests pass. **Pipeline registration deferred to P0-047.1** when the audit-service implementation lands. Reopen when wired into `AddApplication.PipelineBehaviors`.
- `[x]` **P0-025** Centralised exception middleware → RFC 7807 ProblemDetails
  ↳ verify: [`ValidationExceptionHandler.cs`](./apps/api/src/EarlyEagles.Api/ExceptionHandling/ValidationExceptionHandler.cs) maps FluentValidation → 400 `ValidationProblemDetails` with grouped `errors`; [`GlobalExceptionHandler.cs`](./apps/api/src/EarlyEagles.Api/ExceptionHandling/GlobalExceptionHandler.cs) maps everything else → 500 ProblemDetails without leaking message/stack; both wired in [`Program.cs`](./apps/api/src/EarlyEagles.Api/Program.cs) via `AddExceptionHandling()` + `UseExceptionHandler()`; 4 integration tests pass
- `[x]` **P0-026.1** `/health` endpoint returns 200 + version + uptime
  ↳ verify: [`HealthEndpoints.cs`](./apps/api/src/EarlyEagles.Api/Health/HealthEndpoints.cs) maps `GET /health` to `{ status, version, uptimeSeconds }`; integration test via `WebApplicationFactory<Program>` asserts 200 + JSON shape
- `[~]` **P0-026.2** `/ready` endpoint returns 200 when DB + Service Bus reachable
  ↳ endpoint shape + framework wired: `app.MapHealthChecks("/ready", ...)` with a custom JSON writer producing `{ status, checks[], totalDurationMs }`; returns 200 + empty `checks[]` until concrete `IHealthCheck` implementations land for the DB (P0-032.2) and Service Bus (P0-058.4). Integration test asserts current 200/Healthy/empty-checks shape.
- `[x]` **P0-027.1** Swashbuckle UI wired; `/swagger` lists endpoints
  ↳ verify: `Swashbuckle.AspNetCore.SwaggerUI` consumes Microsoft's `/openapi/v1.json` doc; both endpoints gated on non-Production env in [`Program.cs`](./apps/api/src/EarlyEagles.Api/Program.cs); 3 integration tests cover doc parses + UI returns HTML + UI is 404 in Production
- `[x]` **P0-027.2** Auth scheme documented in OpenAPI (Clerk JWT bearer)
  ↳ verify: [`BearerSecuritySchemeTransformer.cs`](./apps/api/src/EarlyEagles.Api/OpenApi/BearerSecuritySchemeTransformer.cs) adds `components.securitySchemes.Bearer` (type=http, scheme=bearer, bearerFormat=JWT); registered via `AddOpenApi(o => o.AddDocumentTransformer<...>())`; integration test asserts the shape on the live `/openapi/v1.json`
- `[ ]` **P0-027.3** Tenant resolution header documented in OpenAPI spec

### Multi-tenancy core

- `[ ]` **P0-028.1** `Tenant` aggregate skeleton (Id, Slug, DisplayName)
- `[ ]` **P0-028.2** Add `Branding` value object to Tenant
- `[ ]` **P0-028.3** Add `Plan` + `Status` enums + transition guards
- `[ ]` **P0-028.4** Unit tests for Tenant invariants
- `[ ]` **P0-029.1** `TenantMember` entity (TenantId, ClerkUserId, Role)
- `[ ]` **P0-029.2** Unit tests for role-change invariants
- `[ ]` **P0-030.1** `ITenantContext` interface + scoped implementation
- `[ ]` **P0-030.2** Integration test confirms per-request scope
- `[ ]` **P0-031.1** Subdomain extractor utility (parse `<slug>.earlyeagles.club`)
- `[ ]` **P0-031.2** Tenant-resolution middleware (extract → lookup → populate)
- `[ ]` **P0-031.3** Unknown-tenant 404 + missing-subdomain 400 in production
- `[ ]` **P0-031.4** E2E test: `<slug>.earlyeagles.club/whoami` returns the right tenant
- `[ ]` **P0-032.1** `ITenantScoped` marker interface; apply to all tenant entities
- `[ ]` **P0-032.2** `DbContext` with global query filter on `ITenantScoped` types
- `[ ]` **P0-032.3** Integration test: query without `ITenantContext` returns empty
- `[ ]` **P0-033.1** EF migration: create `tenants` table
- `[ ]` **P0-033.2** EF migration: create `tenant_members` table with FK to tenants
- `[ ]` **P0-033.3** EF migration: create `audit_events` table (append-only)
- `[ ]` **P0-033.4** Apply migrations against local Postgres; verify with psql
- `[ ]` **P0-034.1** ★ Testcontainers Postgres setup in `Tests.Integration`
- `[ ]` **P0-034.2** ★ Seed fixture: two tenants (Alpha, Beta) with sample data
- `[ ]` **P0-034.3** ★ Test: query as Tenant Alpha, assert Beta's rows not visible
- `[ ]` **P0-034.4** ★ Test: insert as Tenant Alpha, TenantId auto-populates
- `[ ]` **P0-034.5** ★ Test: bypass tenant context, assert empty result
- `[ ]` **P0-034.6** ★ Test: cross-tenant API endpoint access returns 404
- `[ ]` **P0-034.7** ★ Wire isolation tests into CI as required check
- `[ ]` **P0-034.8** ★ Document the test pattern in `docs/runbooks/tenant-isolation-tests.md`

### Identity — Clerk integration

- `[ ]` **P0-035.1** Install `Clerk.Net.SDK`; configure secret key from vault
- `[ ]` **P0-035.2** JWT validation middleware (signature, expiry, audience, issuer)
- `[ ]` **P0-035.3** Integration test: unauth → 401; valid → claims populated
- `[ ]` **P0-036.1** Endpoint `POST /api/webhooks/clerk` accepting raw body
- `[ ]` **P0-036.2** Handle `organization.created` → upsert `tenants` row
- `[ ]` **P0-036.3** Handle `organizationMembership.created` → upsert `tenant_members`
- `[ ]` **P0-036.4** Handle `user.created` → seed basic profile
- `[ ]` **P0-036.5** Handle `user.updated` → patch profile
- `[ ]` **P0-037** Svix signature verification on webhook endpoint
- `[ ]` **P0-038** Map Clerk org roles ↔ `tenant-admin` / `member` claims
- `[ ]` **P0-039.1** Install `@clerk/nextjs`; configure publishable key
- `[ ]` **P0-039.2** Wrap root layout in `<ClerkProvider>`
- `[ ]` **P0-039.3** Add `/sign-in` and `/sign-up` routes using Clerk hosted UI
- `[ ]` **P0-039.4** Next.js middleware redirects unauth users to `/sign-in`
- `[ ]` **P0-040** Sign-in methods: Google + email magic link + phone OTP

### Billing — Stripe integration (scaffolding only)

- `[ ]` **P0-041.1** Install `Stripe.net` + `@stripe/stripe-js`; configure keys
- `[ ]` **P0-041.2** Stripe CLI installed locally for webhook testing
- `[ ]` **P0-042.1** Stripe dashboard: Free product + $0/mo price (test mode)
- `[ ]` **P0-042.2** Stripe dashboard: Pro product + $25/mo price (test mode)
- `[ ]` **P0-042.3** Stripe dashboard: Enterprise product + placeholder price
- `[ ]` **P0-043.1** Endpoint `POST /api/webhooks/stripe` accepting raw body
- `[ ]` **P0-043.2** Handle `checkout.session.completed` → `SubscriptionStarted` event
- `[ ]` **P0-043.3** Handle `customer.subscription.*` family → state-change events
- `[ ]` **P0-043.4** Handle `invoice.payment_failed` → `PaymentFailed` event
- `[ ]` **P0-044** Stripe webhook signature verification
- `[ ]` **P0-045.1** Migration: add `stripe_customer_id`, `stripe_subscription_id` to `tenants`
- `[ ]` **P0-045.2** Subscription event → look up tenant → update Plan
- `[ ]` **P0-045.3** Integration test: simulated webhook updates tenant plan

### Audit logging

- `[ ]` **P0-046.1** `AuditEvent` entity
- `[ ]` **P0-046.2** Postgres trigger / check prevents UPDATE/DELETE on `audit_events`
- `[ ]` **P0-047.1** `IAuditService.Log(action, resourceType, resourceId, metadata)`
- `[ ]` **P0-047.2** `AuditBehavior` wires service to Mediator pipeline
- `[ ]` **P0-048** Audit query endpoint (tenant-admin scope) with pagination

### Domain events + Service Bus

- `[ ]` **P0-049.1** Migration: create `outbox` table
- `[ ]` **P0-049.2** `IDomainEventDispatcher` writes events to outbox in same TX as state
- `[ ]` **P0-049.3** Integration test: failed transaction discards outbox row
- `[ ]` **P0-050.1** Background `IHostedService` polls outbox for unprocessed rows
- `[ ]` **P0-050.2** Publish outbox row to Service Bus topic; mark row processed
- `[ ]` **P0-050.3** Integration test: outbox row → Service Bus message received
- `[ ]` **P0-051** Service Bus topic + subscriptions provisioned (P0-058.4)
- `[ ]` **P0-052.1** `PaymentConfirmedHandler` subscribes to topic
- `[ ]` **P0-052.2** Handler logs the event; integration test verifies firing

### Email + notifications scaffolding

- `[ ]` **P0-053.1** `IEmailSender` interface + `ResendEmailSender` implementation
- `[ ]` **P0-053.2** Integration test: send via Resend; mock verifies request shape
- `[-]` **P0-054** Email template rendering with i18n — deferred per [ADR-0003](docs/adr/0003-defer-i18n.md). English-only templates.
- `[ ]` **P0-055** Smoke-test email from dev → owner inbox

### Infrastructure as Code (Bicep)

- `[ ]` **P0-056** Naming convention + tagging doc (`docs/infra-conventions.md`)
- `[ ]` **P0-057.1** Bicep module: `resourceGroup.bicep`
- `[ ]` **P0-057.2** Bicep module: `keyVault.bicep`
- `[ ]` **P0-057.3** Bicep module: `logAnalytics.bicep`
- `[ ]` **P0-057.4** Bicep module: `appInsights.bicep`
- `[ ]` **P0-058.1** Bicep module: `postgres-flexible.bicep`
- `[ ]` **P0-058.2** Bicep module: `containerAppsEnv.bicep`
- `[ ]` **P0-058.3** Bicep module: `containerApp.bicep`
- `[ ]` **P0-058.4** Bicep module: `serviceBus.bicep`
- `[ ]` **P0-058.5** Bicep module: `redis.bicep`
- `[ ]` **P0-059.1** `infra/main.dev.bicep` orchestrating all modules
- `[ ]` **P0-059.2** `infra/main.dev.bicepparam` with dev values
- `[ ]` **P0-059.3** Deploy `main.dev.bicep` to throwaway RG; verify healthy
- `[ ]` **P0-060** `infra/main.staging.bicep`
- `[ ]` **P0-061** `infra/main.prod.bicep` (empty, no traffic)
- `[ ]` **P0-062.1** Assign managed identity to Container App
- `[ ]` **P0-062.2** Grant identity access to Postgres (Azure AD auth)
- `[ ]` **P0-062.3** Grant identity access to Key Vault + Service Bus
- `[ ]` **P0-063** `docs/runbooks/deploy-infra.md`

### Frontend foundations

- `[x]` **P0-064** `apps/web` Next.js 16 + TypeScript + ESLint scaffold
  ↳ verify: `npm run dev` from repo root boots; page renders 200
- `[x]` **P0-065** Tailwind v4 + shadcn/ui (Base UI) + base components
  ↳ verify: `components.json` + `components/ui/{button,card,chart}.tsx` present
- `[-]` **P0-066** `next-intl` + locale routing + bn messages — deferred per [ADR-0003](docs/adr/0003-defer-i18n.md)
- `[ ]` **P0-067.1** Install `next-pwa`; configure in `next.config.ts`
- `[ ]` **P0-067.2** `public/manifest.webmanifest` with icon set + theme color
- `[ ]` **P0-067.3** Verify install on Chrome DevTools + Android home screen
- `[ ]` **P0-068.1** Generate VAPID keys; store public in env, private in vault
- `[ ]` **P0-068.2** Service worker registers push subscription
- `[ ]` **P0-068.3** API endpoint `POST /api/push/subscribe` stores subscription per user
- `[ ]` **P0-068.4** Integration test: subscribe → endpoint → DB row exists
- `[-]` **P0-069** Bangla font — deferred per [ADR-0003](docs/adr/0003-defer-i18n.md)
- `[ ]` **P0-070.1** Top-bar component (masthead with logo slot + user dropdown)
- `[ ]` **P0-070.2** Bottom-nav for mobile (Home / Wall / Roster / Events / Chat / Settings)
- `[ ]` **P0-070.3** Theme switcher (light / dark / system) wired to `.light` class
- `[ ]` **P0-070.4** Responsive layout tests at phone / tablet / desktop breakpoints
  ↳ scope note: per [ADR-0004](docs/adr/0004-role-based-landing-and-routing.md) the eventual nav splits into member (`/`, `/roster`, `/events`, `/chat`, `/settings`) and admin (`/admin`, `/insights`). Real implementation in Phase 1.
- `[ ]` **P0-071.1** Install `@tanstack/react-query`; set up `QueryClientProvider`
- `[ ]` **P0-071.2** Auth-aware fetch wrapper injects Clerk JWT
- `[ ]` **P0-071.3** Sample authenticated query renders data from `/api/whoami`
- `[ ]` **P0-072** Zustand store for client-only state
- `[ ]` **P0-073.1** Install `@sentry/nextjs`; configure DSN from env
- `[ ]` **P0-073.2** Test error in dev appears in Sentry within 1 min
- `[ ]` **P0-074** PostHog SDK installed + page-view tracking
- `[ ]` **P0-075** Flagsmith SDK installed + sample flag toggle
- `[ ]` **P0-076.1** Cookie consent banner + cookie state persistence
- `[ ]` **P0-076.2** `docs/cookie-policy.md` stub
- `[ ]` **P0-077.1** Next.js middleware extracts subdomain from host
- `[ ]` **P0-077.2** Pass tenant context to pages via React Context
- `[ ]` **P0-077.3** Dev hosts file note in README; `<slug>.localhost:3000` resolves

### CI/CD

- `[ ]` **P0-078.1** GH Actions: `dotnet build` + unit tests on PR
- `[ ]` **P0-078.2** GH Actions: `npm run lint` + `npm run build` on PR
- `[ ]` **P0-079** GH Actions: integration tests against ephemeral Postgres
- `[ ]` **P0-080** Dependabot config for NuGet + npm
- `[ ]` **P0-081** GitHub secret scanning + push protection enabled
- `[ ]` **P0-082** CodeQL workflow for C# + TypeScript
- `[ ]` **P0-083** SBOM (CycloneDX) generated on release
- `[ ]` **P0-084** GH Actions: deploy backend to dev on `main` merge
- `[ ]` **P0-085** GH Actions: deploy frontend to Vercel preview on PR
- `[ ]` **P0-086** GH Actions: deploy to staging on tag (`v*-staging`)
- `[ ]` **P0-087** GH Actions: deploy to prod on manual approval
- `[ ]` **P0-088** Migrations runner job (separate from app deploy)

### Observability + ops

- `[ ]` **P0-089.1** Install OpenTelemetry; configure exporter to Azure Monitor
- `[ ]` **P0-089.2** Enrich spans with `tenant_id` + `user_id`
- `[ ]` **P0-090** Serilog structured logging with `tenant_id` enricher
- `[ ]` **P0-091** App Insights dashboard (RPS, p95, errors, DB calls)
- `[ ]` **P0-092** Better Stack uptime checks → `/health` on dev/staging/prod
- `[ ]` **P0-093** Better Stack public status page at `status.earlyeagles.club`
- `[ ]` **P0-094** Error-budget + alerting thresholds doc

### Documentation

- `[ ]` **P0-095** README quick start (local dev)
- `[ ]` **P0-096** Architecture diagram (mermaid) in README
- `[ ]` **P0-097.1** Runbook: `docs/runbooks/deploy.md`
- `[ ]` **P0-097.2** Runbook: `docs/runbooks/rollback.md`
- `[ ]` **P0-097.3** Runbook: `docs/runbooks/db-migrate.md`
- `[ ]` **P0-097.4** Runbook: `docs/runbooks/incident-response.md`
- `[ ]` **P0-098** Contribution guide + commit format + PR template
- `[ ]` **P0-099** Glossary `docs/glossary.md`

### Bonus / out-of-plan work done so far

- `[x]` **CLAUDE.md** for AI agent context (was P0-D01)
- `[x]` Athletic-modern dashboard at `/` (placeholder until P0-070.1..4 + Phase 1 build the real surfaces)
- `[x]` Editorial primitives + Motion reveal in `apps/web/components/editorial/`
- `[x]` Chart components for attendance / dues / member growth (sample data)
- `[x]` PROGRESS.md (this file)
- `[x]` ADR-0003 (defer i18n), ADR-0004 (role-based landing), ADR-0005 (light social layer)
- `[x]` `docs/design/member-landing.md` (wireframe for Phase 1 implementer)
- `[x]` `.claude/skills/deploy/SKILL.md` (deploy workflow)

### Phase 0 acceptance gate

All of these must be true before declaring Phase 0 done:

- `[ ]` `dotnet test` is green for unit + integration + acceptance suites
- `[ ]` Tenant isolation test (P0-034) is in CI and green
- `[ ]` dev + staging + prod envs all deploy from main via GH Actions
- `[ ]` Audit row is written for every state-changing command
- `[ ]` Authenticated user can sign in via Clerk in dev and reach a tenant-scoped page
- `[ ]` Sentry + App Insights show traffic from a dev smoke test
- `[ ]` Stripe webhook flows through to a Plan change in `tenants`

---

## Phase 1 — Attendance core (Tenant 0 only)  ▱▱▱▱▱▱▱▱▱▱  0%

**Goal:** Make the Sunday attendance workflow real for Early Eagles. Domain
(Member, Session, Attendance, Payment), API endpoints, frontend pages, tests.

Roughly 30 tasks. Detail in [Implementation-Plan.md](Implementation-Plan.md#phase-1--attendance-core-tenant-0-only) — will expand into PROGRESS.md when Phase 0 acceptance is met.

**This is also when the role-based landing pages land** per [ADR-0004](docs/adr/0004-role-based-landing-and-routing.md):

- Build the *real* member `/` (RSVP-first hero, dues status, attendance, latest announcement)
- Build admin `/admin` (full RSVP grid, unpaid-dues list, announcement composer)
- Move the current chart dashboard from `/` to `/insights` (admin-only)
- Clerk middleware redirects tenant-admins to `/admin` on login

## Phase 2 — SaaS commercialization  ▱▱▱▱▱▱▱▱▱▱  0%

Public-facing surface (landing page, pricing, terms / privacy), tenant onboarding wizard, white-label settings, admin consoles, go-live quality bar. ~25 tasks.

## Phase 3 — Real-time chat  ▱▱▱▱▱▱▱▱▱▱  0%

In-app chat via SignalR. Per-tenant rooms, DMs, message history, basic moderation, plus the **reactions implementation** that ADR-0005 added. ~19 tasks. Detail: [Implementation-Plan §Phase 3](./Implementation-Plan.md#phase-3--real-time-chat). Related: [ADR-0005](./docs/adr/0005-light-social-layer.md).

## Phase 4 — Events + guests + Club photo wall  ▱▱▱▱▱▱▱▱▱▱  0%

Picnic / fishing / tournament events, family RSVP, guest signup, event photo gallery, plus the **club-wide photo wall**, **reactions on photos**, and **announcement comments** added by ADR-0005. Also the place where the role-based landing pages from [ADR-0004](./docs/adr/0004-role-based-landing-and-routing.md) get their `/wall` route. ~32 tasks. Detail: [Implementation-Plan §Phase 4](./Implementation-Plan.md#phase-4--events--guests).

## Phase 5 — AI Coach  ▱▱▱▱▱▱▱▱▱▱  0%

Provider abstraction (OpenAI + Anthropic), persona library, drill generator, RAG over BWF rules, per-tenant cost tracking. ~10 tasks. **Differentiation feature** — gates Pro tier in pricing.

## Phase 6 — Mobile app  ▱▱▱▱▱▱▱▱▱▱  0%

Native iOS + Android via Flutter or Expo (decision deferred). ~30 tasks.

## Phase 7 — Enterprise tier  ▱▱▱▱▱▱▱▱▱▱  0%

SSO (SAML/OIDC), SCIM, advanced audit + retention, dedicated DB option, region residency, SOC 2 readiness posture. ~15 tasks.

## Phase 8 — Microsoft 365 / Teams Agent (optional)  ▱▱▱▱▱▱▱▱▱▱  0%

Teams-native surface for enterprise customers using Teams. Decide whether to do Phase 8 only after Phase 7 + at least one prospect requires Teams. ~10 tasks.

## Phase 9 — XR / MatchCourt (long-horizon)  ▱▱▱▱▱▱▱▱▱▱  0%

Unity + Meta XR virtual clubhouse. Not planned in detail until ≥50 paying tenants.

---

## Sources of truth

- [Implementation-Plan.md](./Implementation-Plan.md) — full task list, dependencies, acceptance criteria, effort estimates
- [Plan.md](./Plan.md) — architecture, stack table, cost model, 9-phase roadmap
- [project-scope.md](./project-scope.md) — what the platform is, who it serves, what each phase delivers
- [docs/adr/](./docs/adr/) — architecture decisions (immutable history)
- [CLAUDE.md](./CLAUDE.md) — conventions + load-bearing context for Claude Code sessions

When a task ID here drifts from the plan, the plan wins — fix here, not there.

## Conventions for this file

- Update status when you finish (or block on) a task — same commit as the code.
- Don't delete deferred tasks; mark them `[-]` and link the ADR that deferred them.
- New tasks introduced mid-phase append to the bottom of their section (don't renumber).
- The "Phase 0 acceptance gate" checklist is the only place that controls whether Phase 0 is done — not the percentage bar above.

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

Last updated: 2026-09-21

---

## Phase 0 — Enterprise Foundations  ▰▰▰▱▱▱▱▱▱▱  ~37% (70 / ~190 tasks)

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
- `[x]` **P0-024.3** `TransactionBehavior` Mediator pipeline (wraps writes in TX)
  ↳ verify: [`EfUnitOfWork.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/EfUnitOfWork.cs) — the concrete `IUnitOfWork` the behavior was waiting on. Scoped, wraps the request's `ApplicationDbContext` connection via `Database.BeginTransactionAsync`; `EfTransaction.DisposeAsync` is what rolls back, since `TransactionBehavior` never calls Rollback explicitly — it relies on `await using` disposing an uncommitted handle. **Nested begins join rather than throw**: Npgsql has no nested transactions, so a command dispatched from inside another command's handler would make EF throw on the second `Begin`; `JoinedTransaction` makes the inner commit a no-op and the inner dispose a no-op, leaving the outermost scope the sole decider (inner success never commits work the outer scope may yet abandon; inner failure still rolls back everything). The audit log deliberately does *not* participate — `AuditService` uses its own context on its own connection (P0-047.1).
  **Behavior now gated to `ICommand`** (same cached-reflection gate `AuditBehavior` uses) — the task says "wraps *writes*", and opening a transaction around a read is a wasted round trip. Queries and plain `IRequest` pass through.
  **Now registered**: `AddApplication` pipeline is `[Logging, Audit, Validation, Transaction]` — Transaction innermost, so a validation failure throws before any transaction opens and the audit row lands outside the transaction that just rolled back. `AddInfrastructure` binds `IUnitOfWork -> EfUnitOfWork` scoped.
  Tests: [`TransactionBehaviorTests.cs`](./apps/api/tests/EarlyEagles.Tests.Unit/Behaviors/TransactionBehaviorTests.cs) grew to 7 (added query pass-through, plain-`IRequest` pass-through, non-generic `ICommand` is wrapped — Mediator's `ICommand` and `ICommand<T>` are separate interfaces). [`EfUnitOfWorkTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/EfUnitOfWorkTests.cs) — 6 real-Postgres tests proving the EF layer actually commits and rolls back: commit persists; dispose-without-commit discards; a handler that writes then throws (driven through the real behavior) leaves nothing; a successful command commits; a nested join isn't durable until the outer commits; a nested join is undone when the outer abandons. Reads verify on a **separate** context so they only see committed rows. 189 unit + 70 integration pass.
- `[x]` **P0-024.4** `AuditBehavior` Mediator pipeline (writes AuditEvent — wires to P0-047)
  ↳ verify: behavior + `IAuditService` / `AuditEntry` abstractions were written earlier; 5 unit tests still pass with a fake `CapturingAuditService`. As of P0-047.2 (below), it is **live in the pipeline**: `AddApplication` registers `[LoggingBehavior<,>, AuditBehavior<,>, ValidationBehavior<,>, TransactionBehavior<,>]` (Transaction added by P0-024.3) and `AddInfrastructure` binds `IAuditService` to the real `AuditService`. Every `ICommand` dispatched through Mediator now produces an audit row.
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
- `[x]` **P0-027.3** Tenant resolution documented in OpenAPI spec (subdomain primary + `X-Tenant-Slug` fallback)
  ↳ verify: [`TenantResolutionTransformer.cs`](./apps/api/src/EarlyEagles.Api/OpenApi/TenantResolutionTransformer.cs) adds `Info.Description` explaining the dual mechanism + a `{tenant}.earlyeagles.club` server template; [`TenantHeaderOperationTransformer.cs`](./apps/api/src/EarlyEagles.Api/OpenApi/TenantHeaderOperationTransformer.cs) adds the optional `X-Tenant-Slug` header to every operation; 3 integration tests cover description text, server template, and per-operation parameter shape

### Multi-tenancy core

- `[x]` **P0-028.1** `Tenant` aggregate skeleton (Id, Slug, DisplayName)
  ↳ verify: [`Tenant.cs`](./apps/api/src/EarlyEagles.Domain/Tenants/Tenant.cs) — sealed `AggregateRoot<Guid>`; private ctor + `Create(id, slug, displayName)` factory enforces RFC 1035 slug rules (2–63 ASCII chars, lowercase letter or digit, hyphens allowed mid-string only) + trimmed non-empty display name ≤200 chars; 16 TenantTests pass (50 total unit tests)
- `[x]` **P0-028.2** Add `Branding` value object to Tenant
  ↳ verify: [`Branding.cs`](./apps/api/src/EarlyEagles.Domain/Tenants/Branding.cs) — immutable `ValueObject` with `LogoUrl` (absolute http/https URI ≤2048 chars) + `PrimaryColor` + `AccentColor` (`#RGB` / `#RRGGBB` / `#RRGGBBAA` hex, normalised to lowercase for case-insensitive structural equality); `Tenant.Branding` nullable + `Tenant.SetBranding(branding)` replaces wholesale; 23 BrandingTests + 3 SetBranding tests pass (76 total unit tests)
- `[x]` **P0-028.3** Add `Plan` + `Status` enums + transition guards
  ↳ verify: [`TenantPlan.cs`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantPlan.cs) (`Free | Pro | Enterprise`) + [`TenantStatus.cs`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantStatus.cs) (`Active | PastDue | Suspended | Deactivated`); `Tenant.Create` defaults to `Plan=Free, Status=Active`; `ChangePlan` / `MarkPastDue` / `Suspend` / `Reactivate` / `Deactivate` guard against the legal origin states and throw `InvalidOperationException` on bad transitions, no-op on self-transitions so replayed Stripe webhooks are idempotent; 22 TenantTransitionTests pass (98 total unit tests)
- `[x]` **P0-028.4** Unit tests for Tenant invariants
  ↳ verify: [`TenantInvariantsTests.cs`](./apps/api/tests/EarlyEagles.Tests.Unit/Tenants/TenantInvariantsTests.cs) — pins slug uniqueness as a persistence-layer contract (not a domain invariant), exercises a full subscribe→upgrade→fail→recover→suspend→deactivate lifecycle, asserts Plan / Status / Branding are independent fields, and locks in the new `SetBranding`-on-Deactivated guard added for symmetry with `ChangePlan`; 7 invariant tests pass (105 total unit tests)
- `[x]` **P0-029.1** `TenantMember` entity (TenantId, ClerkUserId, Role)
  ↳ verify: [`TenantMember.cs`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantMember.cs) — `Entity<TenantMemberId>` with composite identity `(TenantId, ClerkUserId)` via the [`TenantMemberId`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantMemberId.cs) record struct, so the base `Entity<TId>` equality semantics give `(tenantA, userX) == (tenantA, userX)` and `(tenantA, userX) != (tenantB, userX)` for free; `Role` of type [`TenantMemberRole`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantMemberRole.cs) (`Member | TenantAdmin`); `Create` + `ChangeRole` enforce non-empty TenantId / ClerkUserId (≤256 chars) + defined enum; 13 TenantMemberTests pass (118 total unit tests)
- `[x]` **P0-029.2** Unit tests for role-change invariants
  ↳ verify: [`TenantMemberRoleChangeTests.cs`](./apps/api/tests/EarlyEagles.Tests.Unit/Tenants/TenantMemberRoleChangeTests.cs) — pins "last TenantAdmin cannot be demoted" as an application-service contract (the aggregate has no view of siblings), confirms role changes preserve the `(TenantId, ClerkUserId)` composite identity, alternating promotions/demotions resolve to the latest assignment, and the same Clerk user's role in tenant A is independent of their role in tenant B; 5 invariant tests pass (123 total unit tests)
- `[x]` **P0-030.1** `ITenantContext` interface + scoped implementation
  ↳ verify: reader [`ITenantContext`](./apps/api/src/EarlyEagles.Application/Abstractions/Tenancy/ITenantContext.cs) + setter [`ITenantContextInitializer`](./apps/api/src/EarlyEagles.Application/Abstractions/Tenancy/ITenantContextInitializer.cs) split so feature code can't mutate the ambient tenant mid-request; [`TenantContext`](./apps/api/src/EarlyEagles.Infrastructure/Tenancy/TenantContext.cs) implements both — throws `InvalidOperationException` on read-before-init and on double-init, validates non-empty tenantId / slug; [`AddInfrastructure()`](./apps/api/src/EarlyEagles.Infrastructure/DependencyInjection.cs) (new infra DI entry, wired in `Program.cs` after `AddApplication()`) registers the concrete `TenantContext` scoped, with both interfaces resolving to the same instance via factory; 11 TenantContextTests cover the impl contract + DI shape (same-instance-per-scope, fresh-instance-per-scope-boundary); 134 total unit tests + 13 integration tests pass
- `[x]` **P0-030.2** Integration test confirms per-request scope
  ↳ verify: [`TenantContextPerRequestScopeTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Tenancy/TenantContextPerRequestScopeTests.cs) — `WebApplicationFactory<Program>` boots the real pipeline; an `IStartupFilter` prepends a test middleware that calls `Initialize` from request services and pushes the resolved `(reader, setter)` pair into a singleton `CapturedScopes`; two `GET /health` calls produce two reference-distinct `ITenantContext` instances, and within a single request the reader and setter resolve to the same backing object; 2 integration tests pass (15 total)
- `[x]` **P0-031.1** Subdomain extractor utility (parse `<slug>.earlyeagles.club`)
  ↳ verify: [`SubdomainExtractor.cs`](./apps/api/src/EarlyEagles.Infrastructure/Tenancy/SubdomainExtractor.cs) — static `TryExtract(host, out slug)` parser; matches `<slug>.earlyeagles.club` and `<slug>.localhost`, strips `:port` suffixes, lowercases the slug for deterministic DB lookups, IPv6-safe (bracketed form short-circuits without crashing); rejects null/whitespace, bare base domains, foreign hosts, nested subdomains, empty prefixes, IPv4/IPv6 literals; format validation is deliberately deferred to the middleware's DB lookup; 29 SubdomainExtractorTests cover happy + 11 edge classes (163 total unit tests)
- `[x]` **P0-031.2** Tenant-resolution middleware (extract → lookup → populate)
  ↳ verify: [`TenantResolutionMiddleware.cs`](./apps/api/src/EarlyEagles.Api/Tenancy/TenantResolutionMiddleware.cs) — `IMiddleware` that pipes `SubdomainExtractor` → [`ITenantLookup.FindBySlugAsync`](./apps/api/src/EarlyEagles.Application/Abstractions/Tenancy/ITenantLookup.cs) → `ITenantContextInitializer.Initialize`; misses (no subdomain / unknown slug) pass through without erroring (404 / 400 layered on in P0-031.3); stub [`NullTenantLookup`](./apps/api/src/EarlyEagles.Infrastructure/Tenancy/NullTenantLookup.cs) keeps the pipeline wireable until the EF-backed implementation lands with P0-032.2; middleware registered as transient + wired into `Program.cs` after `UseHttpsRedirection`; [`TenantResolutionMiddlewareTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Tenancy/TenantResolutionMiddlewareTests.cs) drives the real `WebApplicationFactory<Program>` pipeline with a `FakeTenantLookup` and an `IStartupFilter` capture that runs *after* `next()` so it observes post-resolution state; 3 integration tests cover known slug (resolved), missing subdomain (unresolved), unknown slug (unresolved without erroring); 163 unit + 18 integration tests pass
- `[x]` **P0-031.3** Unknown-tenant 404 + missing-subdomain 400 in production
  ↳ verify: [`TenantResolutionMiddleware.cs`](./apps/api/src/EarlyEagles.Api/Tenancy/TenantResolutionMiddleware.cs) — resolution stays best-effort; **enforcement** is gated on `IHostEnvironment.IsProduction()` AND path not in the tenant-agnostic allow-list (`/health`, `/ready`, `/openapi`, `/swagger` — infra + uptime probes on the bare domain). In Production a missing subdomain → 400 `Tenant not specified`, an unknown slug → 404 `Tenant not found`, both RFC 7807 ProblemDetails via `IProblemDetailsService` (traceId/instance stay consistent with P0-025). Non-Production stays lenient. [`TenantEnforcementTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Tenancy/TenantEnforcementTests.cs) — 5 tests (prod 400 / prod 404 / prod known-slug passes through resolved / prod `/health` reachable tenant-less / dev leniency); 163 unit + 23 integration pass
- `[x]` **P0-031.4** E2E test: `<slug>.earlyeagles.club/whoami` returns the right tenant
  ↳ verify: [`GET /whoami`](./apps/api/src/EarlyEagles.Api/Tenancy/TenancyEndpoints.cs) — minimal tenant-scoped diagnostic endpoint that echoes `ITenantContext` as [`WhoAmIResponse`](./apps/api/src/EarlyEagles.Api/Tenancy/WhoAmIResponse.cs) `{ tenantId, slug }` when resolved, else 400 `Tenant not resolved`; mapped in `Program.cs` after the health endpoints (so it sits behind the resolution middleware). [`WhoAmIEndpointTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Tenancy/WhoAmIEndpointTests.cs) drives the real `WebApplicationFactory<Program>` in **Production** with a `FakeTenantLookup`: `early-eagles.earlyeagles.club/whoami` → 200 with the resolved `{ tenantId, slug }`, proving subdomain→tenant resolution end-to-end through the full pipeline. The now-live endpoint also tightened two existing `TenantEnforcementTests` (known-slug pass-through no longer relies on a routing 404; dev leniency now asserts the endpoint's own `Tenant not resolved` vs the middleware's `Tenant not specified`); 11 tenancy integration tests pass
- `[x]` **P0-032.1** `ITenantScoped` marker interface; apply to all tenant entities
  ↳ verify: [`ITenantScoped.cs`](./apps/api/src/EarlyEagles.Domain/Primitives/ITenantScoped.cs) — marker with `Guid TenantId { get; }`, the single source of truth the EF Core global query filter (P0-032.2) keys off. Applied to [`Tenant`](./apps/api/src/EarlyEagles.Domain/Tenants/Tenant.cs) (`TenantId => Id`, since a tenant is scoped to itself — the pre-auth resolution lookup bypasses the filter via `IgnoreQueryFilters`) and [`TenantMember`](./apps/api/src/EarlyEagles.Domain/Tenants/TenantMember.cs) (`TenantId => Id.TenantId`, already projected from its composite identity). `AuditEvent` doesn't exist yet — it lands in P0-046.1 and implements the marker there. [`TenantScopedMarkerTests.cs`](./apps/api/tests/EarlyEagles.Tests.Unit/Tenancy/TenantScopedMarkerTests.cs) — 3 tests: `Tenant`/`TenantMember` carry the marker with the right `TenantId`, plus a reflection guard (the standing tenancy gate) asserting **every** concrete `Entity<>` in the Domain assembly implements `ITenantScoped`, so a new entity that forgets it fails the build rather than leaking rows cross-tenant; 166 unit tests pass
- `[x]` **P0-032.2** `DbContext` with global query filter on `ITenantScoped` types
  ↳ verify: [`ApplicationDbContext.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/ApplicationDbContext.cs) — first EF Core 10.0.11 + Npgsql 10.0.3 integration. `OnModelCreating` loops every `ITenantScoped` entity and applies a global query filter `EF.Property<Guid>(e, col) == CurrentTenantId` (col = `TenantId` column, or the PK for the self-scoped `Tenant`). `CurrentTenantId` is snapshotted from `ITenantContext` per request — `Guid.Empty` when unresolved, so a context with no tenant reads nothing. Mappings in [`Persistence/Configurations`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Configurations) (enums→strings, optional owned `Branding`, unique `slug`, `tenant_members` composite PK + FK). **TenantMember was restructured** (per user decision) to a scalar `TenantId`+`ClerkUserId` composite key so the filter emits a real `tenant_id =` predicate and P0-033.2's FK has columns; it stays `Entity<TenantMemberId>` with `Id` derived, and EF binds its constructor on materialisation so `Id` is correct for loaded rows. [`TenantQueryFilterTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantQueryFilterTests.cs) asserts the SQL via `ToQueryString()` (no live DB needed): members filter on `tenant_id`, tenants on `id`, unresolved→`Guid.Empty`, and a cross-instance test proving the filter re-evaluates per context (no cached-model staleness). 166 unit + 28 integration pass. **Deferred:** the EF-backed `ITenantLookup` (still `NullTenantLookup`) and real connection-string config (placeholder for now) wait for a live DB in P0-033/P0-034.
- `[x]` **P0-032.3** Integration test: query without `ITenantContext` returns empty
  ↳ verify: covered by P0-034.5 below — the real-Postgres acceptance test for the unresolved case strictly supersedes a DbContext-only unit test of the same assertion.
- `[x]` **P0-033.1** EF migration: create `tenants` table
  ↳ verify: [`20260827010529_InitialCreate`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Migrations/20260827010529_InitialCreate.cs) — EF Core migration tooling stood up: `dotnet-ef` as a local tool ([`apps/api/.config/dotnet-tools.json`](./apps/api/.config/dotnet-tools.json)), `Microsoft.EntityFrameworkCore.Design` on Infrastructure, and a design-time [`ApplicationDbContextFactory`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/ApplicationDbContextFactory.cs) (supplies an unresolved tenant context so the CLI needn't boot the web host; connection from `ConnectionStrings__Postgres` env var, else the placeholder). Applied against a throwaway `postgres:17` container: `\d tenants` shows `id uuid PK`, `slug varchar(63)` UNIQUE, `display_name`, nullable `branding_*`, `plan`/`status varchar(32)`.
- `[x]` **P0-033.2** EF migration: create `tenant_members` table with FK to tenants
  ↳ verify: same `InitialCreate` migration (both entities were already modelled, so EF's first migration covers both tables — one migration, not two). `\d tenant_members`: composite PK `(tenant_id, clerk_user_id)`, `role varchar(32)`, and FK `FK_tenant_members_tenants_tenant_id (tenant_id) → tenants(id) ON DELETE CASCADE`, confirmed applied in Postgres. 166 unit + 28 integration still green with the migration in the build.
- `[x]` **P0-033.3** EF migration: create `audit_events` table (append-only)
  ↳ verify: [`20260918113105_AddAuditEvents`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Migrations/20260918113105_AddAuditEvents.cs) + [`AuditEventConfiguration.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs). Table: `id uuid PK`, `tenant_id uuid` with FK to `tenants.id` (`ON DELETE RESTRICT` — the log is the durable record of what happened, don't lose it when a tenant row goes away), `action varchar(256)`, `success bool`, `failure_reason varchar(2000) NULL`, `actor_clerk_user_id varchar(256) NULL`, `occurred_at timestamptz`. Composite index `(tenant_id, occurred_at DESC)` serves the primary access pattern (a tenant's recent audit rows, newest first). Applied cleanly against the Testcontainers Postgres — the fixture's `Database.MigrateAsync()` picks it up automatically. `DbSet<AuditEvent>` added to `ApplicationDbContext`; the write-side interceptor (P0-034.4) and read-side global query filter (P0-032.2) both cover it automatically via the `ITenantScoped` marker. Note: [`.editorconfig`](./apps/api/.editorconfig) now silences CA1861 for `Persistence/Migrations/*.cs` — EF codegen emits inline arrays for composite indexes and re-adding the migration would just re-introduce them.
- `[ ]` **P0-033.4** Apply migrations against local Postgres; verify with psql
- `[x]` **P0-034.1** ★ Testcontainers Postgres setup in `Tests.Integration`
  ↳ verify: [`PostgresFixture.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresFixture.cs) — `IAsyncLifetime` fixture starts an ephemeral `postgres:17` via Testcontainers 4.14, applies the real `InitialCreate` migration (`Database.MigrateAsync()`), and exposes `CreateContext(ITenantContext)` so isolation tests (P0-034.2..4) get an `ApplicationDbContext` bound to it. Shared once per collection via [`PostgresCollectionDefinition`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresCollectionDefinition.cs). [`PostgresFixtureSmokeTests`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresFixtureSmokeTests.cs) proves the container is reachable and `InitialCreate` is applied. Also pinned EF Core Relational 10.0.11 in Infrastructure to clear an MSB3277 skew (Npgsql 10.0.3 references 10.0.4). Needs Docker (CI too). 166 unit + 29 integration pass.
- `[x]` **P0-034.2** ★ Seed fixture: two tenants (Alpha, Beta) with sample data
  ↳ verify: [`PostgresFixture.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/PostgresFixture.cs) — after migrating, the fixture seeds tenants **Alpha** (2 members: admin + member) and **Beta** (1 admin) with fixed ids/slugs exposed as constants; inserts aren't touched by the query filter so the unresolved migration context seeds both in one `SaveChanges`. Added a `CreateContext(Guid tenantId, string slug)` overload that resolves the ambient tenant so tests can "operate as" a tenant.
- `[x]` **P0-034.3** ★ Test: query as Tenant Alpha, assert Beta's rows not visible
  ↳ verify: [`TenantIsolationTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantIsolationTests.cs) — **the headline gate**, 5 tests against real Postgres: reading as Alpha returns only Alpha's 2 members; as Beta only Beta's 1; a direct PK lookup of Beta's member as Alpha returns null (filter isn't key-bypassable); `Tenant` is self-scoped (Alpha sees only its own row); and an `IgnoreQueryFilters()` backstop proves Beta's data IS in the DB — the filter is what hides it. 166 unit + 34 integration pass.
- `[x]` **P0-034.4** ★ Test: insert as Tenant Alpha, TenantId auto-populates
  ↳ verify: [`TenantStampingInterceptor.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/TenantStampingInterceptor.cs) — the **write-side half** of isolation (a real feature, not just a test). A stateless `SaveChangesInterceptor` walks the tracked `ITenantScoped` entries on every save and: stamps the ambient tenant onto an insert that has none; rejects an insert with no tenant id when none is resolved; rejects any insert/update/delete naming a different tenant; rejects an update that moves a row between tenants. Writes with an explicit tenant id and **no** resolved tenant are allowed — the write-side twin of `IgnoreQueryFilters()` for seeding/provisioning/background jobs. The self-scoped `Tenant` aggregate is exempt on **insert** only (provisioning predates its own tenant); updates/deletes to it are still constrained. Rejection throws [`CrossTenantWriteException`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/CrossTenantWriteException.cs). Registered from `ApplicationDbContext.OnConfiguring` as a **static singleton** so it applies however the context is built (DI, design-time factory, or a test `new`-ing it) and no caller can forget it — and so EF's internal service-provider cache stays single-entry. Proof: [`TenantWriteStampingTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/TenantWriteStampingTests.cs), 9 tests against real Postgres; disabling the registration fails 6 of them (the 3 “is allowed” cases stay green, as they should). **Known gap:** `ExecuteUpdate`/`ExecuteDelete` never reach `SaveChanges`, so the interceptor cannot see them — they still honour the query filter, so they only touch the ambient tenant unless the caller opts out with `IgnoreQueryFilters()`. 166 unit + 43 integration pass.
- `[x]` **P0-034.5** ★ Test: bypass tenant context, assert empty result
  ↳ verify: [`UnresolvedTenantContextTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/UnresolvedTenantContextTests.cs) — the read-side **default deny** against real Postgres, 4 tests: a context built with `new TenantContext()` (never `Initialize`d, so `IsResolved=false`) snapshots `Guid.Empty` in `ApplicationDbContext.CurrentTenantId`, and the global query filter compares each row's discriminator against it. Reading `TenantMembers` returns empty; reading `Tenants` returns empty (the self-scoped aggregate still keys off `Guid.Empty`); direct PK lookup for a known Alpha member returns null (the filter isn't bypassable by key); `IgnoreQueryFilters()` reveals both Alpha's and Beta's rows — proving the emptiness comes from the filter, not from missing seed data. Closes the "middleware misfires or is bypassed" gap: queries fail closed, not open. 166 unit + 47 integration pass.
- `[x]` **P0-034.6** ★ Test: cross-tenant API endpoint access returns 404
  ↳ verify: [`CrossTenantEndpointTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/CrossTenantEndpointTests.cs) — the isolation gate at the HTTP layer. New minimal endpoint [`GET /tenant-members/{clerkUserId}`](./apps/api/src/EarlyEagles.Api/Tenancy/TenantMembersEndpoints.cs) does a naive `db.TenantMembers.FirstOrDefaultAsync(m => m.ClerkUserId == id)` — the isolation is invisible to the handler, so the 404 is provably the work of the global query filter, not a hand-rolled tenant check. Test boots the real `Program` via `WebApplicationFactory` in Production, swaps `ITenantLookup` for a `FakeTenantLookup` seeded with Alpha + Beta, and swaps `DbContextOptions<ApplicationDbContext>` to point at the Testcontainers Postgres. 5 tests: Alpha requesting own admin → 200 with the member body; Alpha requesting Beta's admin id → **404**; Beta requesting Alpha's member id → **404**; Beta requesting own admin → 200; and an equivalence test proving unknown id and cross-tenant id return identical 404s (a future 403-for-cross-tenant leak would fail here). Runbook `docs/runbooks/tenant-isolation-tests.md` updated in the same change. 166 unit + 52 integration pass.
- `[x]` **P0-034.7** ★ Wire isolation tests into CI as required check
  ↳ verify: [`.github/workflows/backend.yml`](./.github/workflows/backend.yml) — the isolation gate runs in its own `integration tests (tenant-isolation gate)` job on GitHub-hosted `ubuntu-latest` (Docker preinstalled, no service definition needed) on every PR to `main` and every push to `main`. No `paths:` filter — a path-filtered required check never reports on out-of-scope PRs and blocks merges forever; workflow comment explains the trade-off. Job name is deliberate so it can be listed by name in branch protection. **Branch-protection wiring is a repo-settings action** (Settings → Branches → require `integration tests (tenant-isolation gate)` + `build + unit tests`); runbook `docs/runbooks/tenant-isolation-tests.md` §11 walks through it.
- `[x]` **P0-034.8** ★ Document the test pattern in `docs/runbooks/tenant-isolation-tests.md`
  ↳ verify: [`docs/runbooks/tenant-isolation-tests.md`](./docs/runbooks/tenant-isolation-tests.md) — the operator + developer manual for the gate. Explains the three isolation layers (marker → read-side filter → write-side interceptor), lists every test file under `Persistence/` with the invariant it pins, gives the copy-paste `[Collection(...)]` pattern for adding new isolation tests, walks through adding a new tenant-scoped entity without breaking the gate (the unit-test reflection guard fires first if you forget `ITenantScoped`), documents the two legitimate opt-outs (`IgnoreQueryFilters()` for reads, explicit `TenantId` on writes when no tenant is resolved), the accepted `ExecuteUpdate`/`ExecuteDelete` gap, a one-line summary of every assertion, and a triage checklist for when the gate fails. Flags P0-034.7 as the next step: the CI wiring that makes this file's contract enforceable on `main`.

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

- `[x]` **P0-046.1** `AuditEvent` entity
  ↳ verify: [`AuditEvent.cs`](./apps/api/src/EarlyEagles.Domain/Auditing/AuditEvent.cs) — sealed `AggregateRoot<Guid>` implementing `ITenantScoped`. Captures `Action` (the command's type name, e.g. `RecordAttendanceCommand`), `Success`, optional `FailureReason` (present ⇔ `!Success`), optional `ActorClerkUserId` (null for system-originated actions — webhooks, background jobs, migrations), `OccurredAt` (must be UTC — the log is one chronology). No `Update` / `Delete` methods on the type; the append-only nature is a domain contract, with the Postgres trigger in P0-046.2 as the storage-layer backstop. Ambient enrichment (tenant, actor, clock) is the audit-service's job (P0-047), so `Create` requires the values and validates them — the aggregate stays testable without `ITenantContext` / `IClock`. [`AuditEventTests.cs`](./apps/api/tests/EarlyEagles.Tests.Unit/Auditing/AuditEventTests.cs) — 20 tests covering happy path (success + failure shapes), null-actor round-trip, and every rejection branch: empty id / empty tenantId / null / empty / whitespace / overlong `Action`, success-with-failure-reason, failure-without-failure-reason, empty / overlong failure reason, empty / overlong actor, non-UTC timestamp. Reflection guard `TenantScopedMarkerTests` (from P0-032.1) also automatically catches the new entity's marker contract — no change needed there. 186 unit + 52 integration pass.
- `[x]` **P0-046.2** Postgres trigger / check prevents UPDATE/DELETE on `audit_events`
  ↳ verify: same [`20260918113105_AddAuditEvents`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Migrations/20260918113105_AddAuditEvents.cs) migration installs the storage-layer backstop. A `plpgsql` function `audit_events_reject_change()` `RAISE EXCEPTION` with SQLSTATE `P0001`, wired to two triggers: `BEFORE UPDATE OR DELETE ... FOR EACH ROW` (blocks per-row mutations) **and** `BEFORE TRUNCATE ... FOR EACH STATEMENT` (closes the hole where per-row triggers don't fire for `TRUNCATE`). The domain type has no updater methods, but `ExecuteUpdate`/`ExecuteDelete` bypass the change tracker (and thus the write-side interceptor), and hand-rolled SQL bypasses both — the trigger is the last line of defence. [`AuditEventsAppendOnlyTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Persistence/AuditEventsAppendOnlyTests.cs) — 4 tests against real Postgres: insert succeeds; `ExecuteUpdate` on an own-tenant row throws `PostgresException` with "append-only"; `ExecuteDelete` on an own-tenant row throws the same; `TRUNCATE audit_events` via raw SQL throws the same. Down() drops both triggers before the table then drops the function. 186 unit + 56 integration pass.
- `[x]` **P0-047.1** `IAuditService.Log(action, resourceType, resourceId, metadata)`
  ↳ verify: [`AuditService.cs`](./apps/api/src/EarlyEagles.Infrastructure/Auditing/AuditService.cs) — concrete implementation of `IAuditService` (the port drafted in P0-024.4, whose current shape is `LogAsync(AuditEntry, ct)`; the plan's coarser signature is subsumed). Takes `DbContextOptions<ApplicationDbContext>` + `ITenantContext`. Each `LogAsync` call opens its **own fresh** `ApplicationDbContext` with an **unresolved** `TenantContext` — the audit row carries the tenant id explicitly, and the write-side interceptor's "explicit tenant + no ambient" branch (the platform-work escape hatch from P0-034.4) allows the write. A fresh context gets a fresh pool connection with no ambient TX, so its `SaveChanges` auto-commits **regardless of the outer request's transaction**. That's the whole point: a rolled-back command still leaves a trail. Ambient tenant is required (throws `InvalidOperationException` when unresolved — commands only run inside tenant-scoped requests; an unresolved context here is a bug upstream). Actor is currently null; wired when `ICurrentUser` lands with Clerk (P0-035). [`AuditServiceTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Auditing/AuditServiceTests.cs) — 5 real-Postgres tests: success row persists scoped to ambient tenant; failure row carries the reason; Beta's write is invisible to Alpha (tenant isolation end-to-end for audit rows); unresolved tenant throws; **a request-scope rollback does NOT undo the audit save** (the load-bearing behavioural claim, proven by opening a BEGIN...ROLLBACK on a separate context while calling `LogAsync` inside it and reading the row back after the rollback). 186 unit + 61 integration pass.
- `[x]` **P0-047.2** `AuditBehavior` wires service to Mediator pipeline
  ↳ verify: [`Application/DependencyInjection.cs`](./apps/api/src/EarlyEagles.Application/DependencyInjection.cs) — the Mediator pipeline registration is now `[LoggingBehavior<,>, AuditBehavior<,>, ValidationBehavior<,>]`. Audit sits **outside** where Transaction will eventually go, so failed commands still leave a trail (the load-bearing pipeline-ordering decision from ADR-0002 and the AuditBehavior doc). [`Infrastructure/DependencyInjection.cs`](./apps/api/src/EarlyEagles.Infrastructure/DependencyInjection.cs) binds `IAuditService -> AuditService` scoped. `AuditBehavior`'s XML doc updated: no longer says "not registered yet". Existing 5 unit tests in `AuditBehaviorTests.cs` (from P0-024.4) already prove the wiring's semantics (success logs, failure logs + rethrows, non-command bypasses, cancellation bypasses) — no new pipeline test needed until the first real command exists to send through end-to-end.
- `[ ]` **P0-048** Audit query endpoint (tenant-admin scope) with pagination

### Domain events + Service Bus

- `[x]` **P0-049.1** Migration: create `outbox` table
  ↳ verify: [`OutboxMessage.cs`](./apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxMessage.cs) — persistence machinery so lives in Infrastructure (not Domain), but implements `ITenantScoped` so the read-side global query filter (P0-032.2) and the write-side stamping interceptor (P0-034.4) both cover it without per-caller code. Fields: `Id`, `TenantId`, `EventType` (CLR full name, no assembly info — version-tolerant routing to the bus subscriber), `Payload` (JSON as `text`, no length cap), `OccurredAt` (UTC-only), `ProcessedAt` (nullable — null = pending), `AttemptCount` (default 0), `LastError` (nullable). Static `Create` factory validates each field like the other aggregates. [`OutboxMessageConfiguration.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs) maps to `outbox_messages` with a **partial index** `IX_outbox_messages_pending ON (occurred_at) WHERE processed_at IS NULL` — the poller's working set stays tiny regardless of history size (processed rows dominate over time). [`20260918160757_AddOutboxMessages`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Migrations/20260918160757_AddOutboxMessages.cs) applied cleanly against Testcontainers Postgres. [`OutboxMessageIsolationTests.cs`](./apps/api/tests/EarlyEagles.Tests.Integration/Outbox/OutboxMessageIsolationTests.cs) — 3 tests pinning the equivalent of the domain reflection guard: insert-as-Alpha visible only to Alpha; insert with no tenant + no ambient → `CrossTenantWriteException` (write-side interceptor); `IgnoreQueryFilters()` reveals both tenants' rows (the platform-work opt-out the P0-050 hosted-service publisher will use). 186 unit + 64 integration pass.
- `[x]` **P0-049.2** `IDomainEventDispatcher` writes events to outbox in same TX as state
  ↳ verify: [`IDomainEventDispatcher.cs`](./apps/api/src/EarlyEagles.Application/Abstractions/Messaging/IDomainEventDispatcher.cs) — Application port, **synchronous and void on purpose**: dispatching *stages*, it does not publish, so there is no bus call and no round trip to await. Tenant is a parameter rather than read from `ITenantContext`, because platform work (provisioning a tenant) dispatches with no ambient tenant. [`OutboxDomainEventDispatcher.cs`](./apps/api/src/EarlyEagles.Infrastructure/Outbox/OutboxDomainEventDispatcher.cs) maps each event to an `OutboxMessage` and `Add`s it to the **same `ApplicationDbContext`** carrying the state change — it never calls `SaveChanges`, which is what makes the row and the state one transaction. Deliberate contrast with `AuditService` (P0-047.1), which uses its own connection precisely so it *survives* a rollback; the outbox must not. Row PK reuses `IDomainEvent.Id`, so a queued message stays traceable to its event and a double dispatch fails on the unique key instead of publishing twice. Payload is web-JSON of the **runtime** type (serialising as `IDomainEvent` would silently drop every property), `OccurredAt` normalised to UTC.
  Drained automatically by [`DomainEventOutboxInterceptor.cs`](./apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/DomainEventOutboxInterceptor.cs) — sweeps `ChangeTracker` on `SavingChanges`, dispatches each aggregate's buffer, clears it. Registered from `OnConfiguring` **ahead of** `TenantStampingInterceptor` so staged rows still pass under the write-side guard, and as a stateless static singleton so no way of building a context can skip it. New [`IHasDomainEvents`](./apps/api/src/EarlyEagles.Domain/Primitives/IHasDomainEvents.cs) is the non-generic seam the sweep matches on (`AggregateRoot<TId>` is generic in its id type); raising stays `protected`. Tenant attribution: the aggregate's own `TenantId`, else the ambient tenant, else throw — never a row belonging to nobody.
  11 new unit tests (drain rules with a recording dispatcher; event→row mapping without a database) + 3 new Postgres tests (interceptor is registered on every context; tenant + outbox row commit together through `EfUnitOfWork`; a dispatched event is invisible to the other tenant but reachable via `IgnoreQueryFilters`). 200 unit + 73 integration pass.
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

- `[x]` **P0-078.1** GH Actions: `dotnet build` + unit tests on PR
  ↳ verify: [`.github/workflows/backend.yml`](./.github/workflows/backend.yml) — the first CI workflow. Two parallel jobs so failures are attributable and each can be marked required in branch protection independently: `build + unit tests` (the fast lane — compiles the whole `EarlyEagles.slnx` in Release with warnings-as-errors, then runs the Unit test project) and `integration tests (tenant-isolation gate)` (Testcontainers-backed, the P0-034 gate). Both install .NET 10 SDK via `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` (TFM pinned in `apps/api/Directory.Build.props` per ADR-0006); no `global.json` yet. Concurrency group cancels superseded runs on non-main refs. Minimum-privilege `permissions: contents: read`. Frontend CI (P0-078.2) will be a separate workflow so a Next.js change doesn't wait on a .NET build.
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
- `[x]` Security: pinned `Microsoft.OpenApi` → 2.7.5 to override the vulnerable 2.0.0 pulled transitively by `Microsoft.AspNetCore.OpenApi` (CVE-2026-49451 / GHSA-v5pm-xwqc-g5wc). Revisit when the framework's transitive pin moves past 2.7.5.

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

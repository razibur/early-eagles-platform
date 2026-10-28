# Early Eagles — Implementation Plan

> Concrete, sequenced task list for building the platform per [`Plan.md`](./Plan.md) and [ADR-0002](./docs/adr/0002-enterprise-saas-reframe.md).
>
> **Phases 0–2 are detailed (the next ~12–18 months of work).** Phases 3–9 are deliberately high-level — they will be re-planned closer to the time, when the foundations are settled and real tenant feedback is in.

## Conventions

- **Task IDs:** `P{phase}-{number}` — e.g., `P0-001`, `P1-014`. Stable forever.
- **Effort:**
  - **S** — 1–3 hours (single sitting)
  - **M** — 4–8 hours (one focused day)
  - **L** — 1–2 days
  - **XL** — more than 2 days → break it down further
- **Depends on:** task IDs that must be done first (or `—` if none).
- **Status:** Not started / In progress / Done / Blocked. Track on a board (GitHub Projects) — this file is not the live tracker.
- **Acceptance:** one-line description of "done."

## Cross-cutting tasks (continuous, every phase)

| ID | Task | When |
|---|---|---|
| CC-001 | Write an ADR for any non-trivial decision | Whenever architecture or stack changes |
| CC-002 | Update OpenAPI spec; regenerate clients | Every backend endpoint change |
| CC-003 | Run `dotnet outdated` + `npm outdated`; review Dependabot PRs | Weekly |
| CC-004 | Review Sentry / App Insights for new error patterns | Weekly |
| CC-005 | Audit log review (sample 10 events for correctness) | Monthly |
| CC-006 | Run isolation tests in CI; investigate any failure immediately | Every PR |
| CC-007 | Cost review (Azure + Vercel + Clerk + Stripe + others) | Monthly |
| CC-008 | Backup / restore drill (verify Postgres PITR works) | Quarterly |
| CC-009 | Security review (secrets, dependencies, exposed endpoints) | Quarterly |
| CC-010 | ToS / Privacy / DPA review with current product reality | Quarterly after Phase 2 |

---

## Phase 0 — Enterprise Foundations

**Goal:** A production-grade platform, fully provisioned across dev/staging/prod, that has zero user-facing features but is ready for any feature to be built tenant-aware from the first commit.

**Estimated effort:** ~190 small tasks (1–3 hours each), several months at sustainable solo pace.

> **Granularity:** Each task is 1–3 hours of work, single concept, independently verifiable, committable alone — per the small-task rule established 2026-05-25. Where a task was previously listed as a bundle (e.g., "EarlyEagles.sln with 7 projects"), it is now decomposed using dotted IDs (`P0-020.1`, `P0-020.2`, …). The high-level ID grouping is preserved so existing references stay readable.

### Pre-work — accounts, domain, secrets

External account setup. Each task is a single account or vault entry. None require code.

| ID | Task | Verify |
|---|---|---|
| P0-001 | Register `earlyeagles.club` via Cloudflare Registrar | `whois earlyeagles.club` shows ownership; auto-renew on |
| P0-002 | Create Cloudflare account + add domain DNS zone | Cloudflare dashboard manages the zone; NS records resolve |
| P0-003 | Create Azure subscription; set billing alert at $50/mo dev cap | Test alert email arrives when threshold simulated |
| P0-004 | Create GitHub org + private repo `early-eagles-platform` | Repo URL resolves; you're owner |
| P0-005 | Set up password-manager vault for project secrets | Vault entry exists, can be unlocked |
| P0-006 | Create Clerk account + production application | Clerk dashboard shows publishable + secret keys |
| P0-007 | Create Stripe account (test mode) + verify business | Stripe dashboard accessible, test mode enabled |
| P0-008 | Create Sentry account + two projects (backend + frontend) | Two DSNs in vault |
| P0-009 | Create Better Stack account + status page placeholder | Status page URL resolves |
| P0-010 | Create PostHog account + project | API key in vault |
| P0-011 | Create Resend account + verify `earlyeagles.club` sending domain | DKIM / SPF passing; test email arrives |
| P0-012 | Create Flagsmith account + project + environment | Dev environment shows in dashboard |
| P0-013 | Create Cloudflare R2 bucket `ee-storage-dev` | `wrangler r2 bucket list` shows bucket; access keys in vault |

### Repository scaffolding

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-014.1 | Create `README.md` with project tagline + tech overview + local-dev quick start | P0-004 | README renders on GitHub repo home |
| P0-014.2 | Add `LICENSE` (MIT or Apache-2.0 — decide) | P0-004 | License visible in GitHub repo header |
| P0-014.3 | Add `.gitignore` covering .NET / Node / Bicep / OS / secrets | P0-004 | Build artifacts don't show in `git status` |
| P0-014.4 | Add `.editorconfig` (tabs vs spaces, line endings, final newline) | P0-004 | VS / Rider / VSCode all respect settings |
| P0-014.5 | Add `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1) | P0-004 | File present, linked from README |
| P0-014.6 | Add `CONTRIBUTING.md` (branch + commit + PR conventions) | P0-004 | File present, linked from README |
| P0-015 | Branch protection on `main`: require PR review, status checks, signed commits | P0-014.1 | Direct push to `main` blocked in GitHub Settings → Branches |
| P0-016 | Add `CODEOWNERS` file mapping owner to all paths | P0-014.1 | Test PR auto-requests you as reviewer |
| P0-017 | Add `docs/adr/0000-template.md` ADR template | P0-014.1 | File exists with template scaffold |
| P0-018 | Move existing planning docs (`Plan.md`, `project-scope.md`, `docs/`) into repo | P0-014.1 | All cross-references resolve |
| P0-019 | Monorepo layout (`apps/web`, `apps/api`, `infra`, `docs`) + workspace config | P0-014.1 | `npm install` from root installs all workspaces |

### Backend — Clean Architecture solution

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-020.1 | Create `apps/api/EarlyEagles.sln` (empty solution) | P0-019 | `dotnet sln list` shows 0 projects |
| P0-020.2 | Add `EarlyEagles.Domain` class library (net10.0 per [ADR-0006](./docs/adr/0006-target-dotnet-10-lts.md)); register in sln | P0-020.1 | Project compiles empty; appears in sln list |
| P0-020.3 | Add `EarlyEagles.Application` class library, reference Domain | P0-020.2 | Project compiles; refs Domain only |
| P0-020.4 | Add `EarlyEagles.Infrastructure` class library, reference Application | P0-020.3 | Project compiles; refs Application only |
| P0-020.5 | Add `EarlyEagles.Api` ASP.NET Core 10 web project, reference Application + Infrastructure | P0-020.4 | `dotnet run --project apps/api/src/EarlyEagles.Api` starts |
| P0-020.6 | Add `EarlyEagles.Tests.Unit` xUnit project | P0-020.3 | Empty test runs; `dotnet test` green |
| P0-020.7 | Add `EarlyEagles.Tests.Integration` xUnit project | P0-020.5 | Empty test runs |
| P0-020.8 | Add `EarlyEagles.Tests.Acceptance` xUnit project | P0-020.5 | Empty test runs |
| P0-020.9 | Add architecture test (NetArchTest) enforcing Domain has no Infrastructure references | P0-020.6 | Test fails when Domain `using EarlyEagles.Infrastructure;` is added |
| P0-020.10 | Whole-solution `dotnet build` returns 0 errors + 0 warnings | P0-020.5..8 | CI-clean output |
| P0-021.1 | `Entity<TId>` base class with Id + equality | P0-020.2 | Two entities with same Id are equal |
| P0-021.2 | `ValueObject` base class with structural equality | P0-020.2 | Value-object record equality tested |
| P0-021.3 | `AggregateRoot` base class with domain-event collection | P0-020.2 | Can raise + dequeue events |
| P0-021.4 | `IDomainEvent` interface + dispatch contract | P0-020.2 | Interface exists; implementations recognised |
| P0-021.5 | Unit tests for Entity / ValueObject / AggregateRoot semantics | P0-021.1..4 | `dotnet test` green for new tests |
| P0-022.1 | Add `StyleCop.Analyzers` + `Microsoft.CodeAnalysis.NetAnalyzers` via Directory.Build.props | P0-020.10 | Analyzers run during build |
| P0-022.2 | Configure StyleCop ruleset + `.editorconfig` rules; turn warnings into errors | P0-022.1 | A deliberate violation breaks `dotnet build` |
| P0-023.1 | Install `martinothamar/Mediator` (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)); `AddMediator` scans Application assembly | P0-020.5 | Sample command handler executes via `IMediator.Send` |
| P0-023.2 | Install FluentValidation; register validators from Application | P0-020.5 | Sample validator returns errors for bad input |
| P0-023.3 | Install `Riok.Mapperly` (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)); source-gen mappers as partial classes | P0-020.5 | Sample mapper class produces DTO → entity mapping |
| P0-024.1 | `LoggingBehavior` Mediator pipeline (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)) — logs request + response with correlation ID | P0-023.1 | Sample command produces structured log line |
| P0-024.2 | `ValidationBehavior` Mediator pipeline (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)) — runs FluentValidation, throws on errors | P0-023.2 | Invalid command yields ValidationException |
| P0-024.3 | `TransactionBehavior` Mediator pipeline (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)) — wraps writes in DbContext transaction | P0-023.1, P0-032.2 | Failed handler rolls back changes |
| P0-024.4 | `AuditBehavior` Mediator pipeline (per [ADR-0007](docs/adr/0007-mediator-mapperly-supersede-mediatr-automapper.md)) — writes AuditEvent row on commands (wires to P0-047) | P0-023.1 | Sample command produces audit row |
| P0-025 | Centralised exception middleware → RFC 7807 ProblemDetails | P0-020.5 | `curl /broken` returns `application/problem+json` with title + status |
| P0-026.1 | `/health` endpoint returns 200 + version + uptime | P0-020.5 | `curl /health` returns 200 JSON |
| P0-026.2 | `/ready` endpoint returns 200 when DB + Service Bus reachable, 503 otherwise | P0-026.1, P0-032.2 | Toggle DB connection → endpoint flips correctly |
| P0-027.1 | Swashbuckle wired; `/swagger` lists endpoints | P0-020.5 | UI loads, endpoints documented |
| P0-027.2 | Auth scheme documented in OpenAPI (Clerk JWT bearer) | P0-027.1, P0-035.2 | Swagger "Authorize" dialog accepts JWT |
| P0-027.3 | Tenant resolution header documented in OpenAPI spec | P0-027.1, P0-031.2 | Subdomain explanation visible in Swagger |

### Multi-tenancy core

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-028.1 | `Tenant` aggregate skeleton (Id, Slug, DisplayName) | P0-021.3 | Aggregate compiles; slug invariant enforced |
| P0-028.2 | Add `Branding` value object (logoUrl, primaryColor, accentColor) to Tenant | P0-028.1 | Branding round-trips through tests |
| P0-028.3 | Add `Plan` + `Status` enums + transition guards to Tenant | P0-028.1 | Invalid transitions throw |
| P0-028.4 | Unit tests for Tenant invariants (slug uniqueness spec, plan transitions, branding rules) | P0-028.1..3 | All tests green |
| P0-029.1 | `TenantMember` entity (TenantId, ClerkUserId, Role enum) | P0-028.1 | Entity compiles, identity by (TenantId, ClerkUserId) |
| P0-029.2 | Unit tests for role-change invariants (last admin can't demote, etc.) | P0-029.1 | Edge cases covered |
| P0-030.1 | `ITenantContext` interface + scoped `TenantContext` implementation | P0-028.1 | DI registration; resolved per request |
| P0-030.2 | Integration test confirms per-request scope (different requests → different context) | P0-030.1 | Scope test green |
| P0-031.1 | Subdomain extractor utility — parse `<slug>.earlyeagles.club` and `<slug>.localhost` | P0-030.1 | Unit-tested for happy + edge paths |
| P0-031.2 | Tenant-resolution middleware — extract subdomain → look up Tenant → populate `ITenantContext` | P0-031.1 | Integration test: subdomain hit reaches handler with correct context |
| P0-031.3 | Unknown-tenant 404 + missing-subdomain 400 in production | P0-031.2 | Bad host returns expected status |
| P0-031.4 | End-to-end test: `<slug>.earlyeagles.club/whoami` returns the correct tenant | P0-031.2 | Test passes in CI |
| P0-032.1 | `ITenantScoped` marker interface; apply to all tenant entities | P0-021.1 | Marker applied to Tenant, TenantMember, AuditEvent |
| P0-032.2 | `ApplicationDbContext` with model-builder global query filter on `ITenantScoped` types | P0-030.1, P0-032.1 | Filter generated SQL includes `tenant_id =` clause |
| P0-032.3 | Integration test: query without ITenantContext returns empty / throws | P0-032.2 | Test green |
| P0-033.1 | EF migration: create `tenants` table | P0-032.2 | Migration applies; `\d tenants` shows expected schema |
| P0-033.2 | EF migration: create `tenant_members` table with FK to tenants | P0-033.1 | Migration applies; FK present |
| P0-033.3 | EF migration: create `audit_events` table (append-only via trigger or check constraint) | P0-033.1 | Migration applies; UPDATE/DELETE throws |
| P0-033.4 | Apply migrations against local Postgres; verify with psql | P0-033.1..3 | All three tables exist with correct constraints |
| P0-034.1 | Testcontainers Postgres setup in `Tests.Integration` project | P0-020.7 | Test fixture starts ephemeral Postgres in CI |
| P0-034.2 | Seed fixture: two tenants (Alpha, Beta) with sample data each | P0-034.1, P0-033.4 | Fixture seeds reliably |
| P0-034.3 | Test: query as Tenant Alpha, assert Beta's rows are not visible | P0-034.2 | Test passes; manually breaking filter makes it fail |
| P0-034.4 | Test: insert as Tenant Alpha, assert TenantId auto-populates from context | P0-034.2 | Test passes |
| P0-034.5 | Test: bypass tenant context, assert empty result (not exception) | P0-034.2 | Test passes |
| P0-034.6 | Test: cross-tenant API endpoint access returns 404 (not 403) | P0-034.2 | Test passes |
| P0-034.7 | Wire `TenantIsolation` test category into CI as required check | P0-034.3..6 | CI fails if any isolation test fails |
| P0-034.8 | Document the test pattern in `docs/runbooks/tenant-isolation-tests.md` | P0-034.7 | Doc reviewed; explains how to add new isolation tests |

### Identity — Clerk integration

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-035.1 | Add `Clerk.Net.SDK` NuGet; configure with secret key from vault | P0-006, P0-020.5 | App boots with Clerk config; secret not in source |
| P0-035.2 | JWT validation middleware: validate signature, expiry, audience, issuer | P0-035.1 | Bad token → 401; valid → claims populated |
| P0-035.3 | Integration test: unauth request → 401; authenticated request includes user + org claims | P0-035.2 | Test green |
| P0-036.1 | Endpoint `POST /api/webhooks/clerk` accepting raw body | P0-035.1 | Endpoint reachable; body preserved |
| P0-036.2 | Handle `organization.created` → upsert `tenants` row | P0-036.1, P0-033.1 | Creating org in Clerk dashboard yields new `tenants` row |
| P0-036.3 | Handle `organizationMembership.created` → upsert `tenant_members` row | P0-036.1, P0-033.2 | Adding member in Clerk → new `tenant_members` row |
| P0-036.4 | Handle `user.created` → seed basic profile | P0-036.1 | New user → profile row |
| P0-036.5 | Handle `user.updated` → patch profile fields | P0-036.1 | Updating email in Clerk → updated row |
| P0-037 | Svix signature verification on webhook endpoint | P0-036.1 | Webhook with bad signature → 400; good → 200 |
| P0-038 | Map Clerk org roles ↔ `tenant-admin` / `member` claims in API authorization | P0-036.3 | Admin endpoint requires `tenant-admin`; member call → 403 |
| P0-039.1 | Install `@clerk/nextjs`; configure publishable key | P0-006, P0-064 (done) | App boots without errors |
| P0-039.2 | Wrap root layout in `<ClerkProvider>` | P0-039.1 | Provider context available |
| P0-039.3 | Add `/sign-in` and `/sign-up` routes using Clerk's hosted UI | P0-039.2 | Sign-in flow completes locally |
| P0-039.4 | Next.js middleware redirects unauth users to `/sign-in` (except public routes) | P0-039.3 | Visiting `/` unauth → redirected |
| P0-040 | Sign-in methods: Google + email magic link + phone OTP (MFA optional) | P0-039.3 | Each method completes a sign-in in dev |

### Billing — Stripe integration (scaffolding only)

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-041.1 | Install `Stripe.net` + `@stripe/stripe-js`; configure keys from vault | P0-007 | SDK boots; `stripe.Customers.list()` succeeds |
| P0-041.2 | Stripe CLI installed locally for webhook testing | P0-007 | `stripe listen` forwards events to local API |
| P0-042.1 | Stripe dashboard: Free product + $0/mo price (test mode) | P0-007 | Product visible; price ID in vault |
| P0-042.2 | Stripe dashboard: Pro product + $25/mo price (test mode) | P0-007 | Product visible; price ID in vault |
| P0-042.3 | Stripe dashboard: Enterprise product + placeholder price | P0-007 | Product visible; "contact sales" note recorded |
| P0-043.1 | Endpoint `POST /api/webhooks/stripe` accepting raw body | P0-041.1 | Endpoint reachable |
| P0-043.2 | Handle `checkout.session.completed` → dispatch `SubscriptionStarted` domain event | P0-043.1, P0-049.2 | Stripe CLI test event yields domain event |
| P0-043.3 | Handle `customer.subscription.*` family → dispatch state-change events | P0-043.1 | Each state mapped to a domain event |
| P0-043.4 | Handle `invoice.payment_failed` → dispatch `PaymentFailed` event | P0-043.1 | Test webhook yields domain event |
| P0-044 | Stripe webhook signature verification | P0-043.1 | Bad signature → 400; good → 200 |
| P0-045.1 | Migration: add `stripe_customer_id`, `stripe_subscription_id` to `tenants` | P0-033.1 | Columns exist + nullable |
| P0-045.2 | Subscription event → look up tenant by `stripe_customer_id` → update `Plan` | P0-043.3, P0-045.1 | Plan changes reflect in `tenants` table |
| P0-045.3 | Integration test: simulated webhook updates tenant plan | P0-045.2 | Test green |

### Audit logging

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-046.1 | `AuditEvent` entity (TenantId, ActorId, Action, ResourceType, ResourceId, Metadata, Timestamp) | P0-021.1 | Entity compiles; tenant-scoped |
| P0-046.2 | Postgres trigger / check constraint prevents UPDATE/DELETE on `audit_events` | P0-033.3 | Attempting UPDATE/DELETE throws |
| P0-047.1 | `IAuditService.Log(action, resourceType, resourceId, metadata)` implementation | P0-046.1 | Service writes row when called directly |
| P0-047.2 | `AuditBehavior` wires `IAuditService` to Mediator command pipeline (extends P0-024.4) | P0-047.1, P0-024.4 | Every command produces an audit row |
| P0-048 | Audit query endpoint (tenant-admin scope only) with pagination | P0-046.1, P0-038 | Tenant-admin can list own audit; non-admin → 403 |

### Domain events + Service Bus

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-049.1 | Migration: create `outbox` table (id, type, payload, created_at, processed_at) | P0-033.1 | Table exists with index on processed_at |
| P0-049.2 | `IDomainEventDispatcher` writes events to outbox in the same transaction as state changes | P0-049.1, P0-032.2 | Integration test: domain event + state change committed together |
| P0-049.3 | Integration test: failed transaction discards outbox row + state change | P0-049.2 | Test green |
| P0-050.1 | Background `IHostedService` polls outbox for unprocessed rows | P0-049.1 | Service registered + running in app |
| P0-050.2 | Publish outbox row to Service Bus topic; mark row processed | P0-050.1, P0-051 | Outbox row appears as message on topic |
| P0-050.3 | Integration test: outbox row → Service Bus message received | P0-050.2 | Test green using Service Bus emulator or testcontainer |
| P0-051 | Service Bus topic + subscriptions provisioned (depends on P0-058.4) | P0-058.4 | `az servicebus topic show` returns the topic |
| P0-052.1 | `PaymentConfirmedHandler` subscribes to topic via Service Bus subscription | P0-051 | Handler registered; subscription created |
| P0-052.2 | Handler logs the event; integration test verifies it fires | P0-052.1 | Publish test event → log line + test green |

### Email + notifications scaffolding

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-053.1 | `IEmailSender` interface + `ResendEmailSender` implementation | P0-011 | Interface defined; impl injectable |
| P0-053.2 | Integration test: send email via Resend; mock verifies request shape | P0-053.1 | Test green |
| P0-054 | Email template rendering with i18n — *deferred per [ADR-0003](./docs/adr/0003-defer-i18n.md)*. English-only templates only. | P0-053.1 | n/a (deferred) |
| P0-055 | Smoke-test email from dev → owner inbox | P0-053.1 | Email arrives in real inbox |

### Infrastructure as Code (Bicep)

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-056 | Naming convention + tagging strategy in `docs/infra-conventions.md` | — | Doc reviewed; explicit rules for env/region/owner tags |
| P0-057.1 | `infra/modules/resourceGroup.bicep` (location, tags) | P0-003 | `bicep build` succeeds |
| P0-057.2 | `infra/modules/keyVault.bicep` (RBAC, soft-delete, purge protection) | P0-057.1 | Module deploys; secret round-trip works |
| P0-057.3 | `infra/modules/logAnalytics.bicep` (retention, SKU) | P0-057.1 | Workspace provisioned |
| P0-057.4 | `infra/modules/appInsights.bicep` (workspace-linked) | P0-057.3 | App Insights linked to Log Analytics |
| P0-058.1 | `infra/modules/postgres-flexible.bicep` (HA, backup retention, AAD admin) | P0-057.1 | DB deploys; `psql` connect succeeds |
| P0-058.2 | `infra/modules/containerAppsEnv.bicep` (Log Analytics, Dapr) | P0-057.3 | Env deploys |
| P0-058.3 | `infra/modules/containerApp.bicep` (image, scale rules, env vars, managed identity) | P0-058.2 | Container app deploys; serves traffic |
| P0-058.4 | `infra/modules/serviceBus.bicep` (namespace + topics + subscriptions) | P0-057.1 | `az servicebus topic show` returns topic |
| P0-058.5 | `infra/modules/redis.bicep` (Standard SKU, network ACLs) | P0-057.1 | `redis-cli ping` returns PONG |
| P0-059.1 | `infra/main.dev.bicep` orchestrating all modules with dev parameters | P0-057.*, P0-058.* | `bicep build` succeeds |
| P0-059.2 | `infra/main.dev.bicepparam` with dev environment values | P0-059.1 | Param file parses |
| P0-059.3 | Deploy `main.dev.bicep` to a throwaway RG; verify all resources healthy | P0-059.1..2 | `az deployment sub create` returns success |
| P0-060 | `infra/main.staging.bicep` (with staging params) | P0-059.1 | Staging env deployed |
| P0-061 | `infra/main.prod.bicep` (with prod params, empty no traffic) | P0-059.1 | Prod env deployed, no traffic routed |
| P0-062.1 | Assign managed identity to Container App | P0-058.3 | Identity ID visible in resource |
| P0-062.2 | Grant managed identity access to Postgres (Azure AD auth) | P0-062.1, P0-058.1 | API connects to DB without password |
| P0-062.3 | Grant managed identity access to Key Vault + Service Bus | P0-062.1, P0-057.2, P0-058.4 | API reads secret + publishes message via identity |
| P0-063 | `docs/runbooks/deploy-infra.md` documenting Bicep deploy | P0-061 | Doc has step-by-step `az deployment` commands |

### Frontend foundations

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-064 | `apps/web` Next.js 16 + TypeScript + ESLint scaffold — **DONE** | — | `npm run dev` boots; page renders 200 |
| P0-065 | Tailwind v4 + shadcn/ui (Base UI) + base components — **DONE** | — | `components.json` + `components/ui/{button,card,chart}.tsx` present |
| P0-066 | `next-intl` + locale routing + bn message files — *deferred per [ADR-0003](./docs/adr/0003-defer-i18n.md)* | — | n/a (deferred) |
| P0-067.1 | Install `next-pwa`; configure in `next.config.ts` | P0-064 | Service worker registers in dev |
| P0-067.2 | `public/manifest.webmanifest` with icon set + theme color | P0-067.1 | Manifest valid in Chrome DevTools |
| P0-067.3 | Verify install: Chrome DevTools → Application → Manifest valid; install icon appears | P0-067.2 | "Add to Home Screen" works on Android |
| P0-068.1 | Generate VAPID keys; store public in env, private in vault | P0-005 | Keys generated; private not in source |
| P0-068.2 | Service worker registers push subscription | P0-067.1, P0-068.1 | Subscription object obtained in browser console |
| P0-068.3 | API endpoint `POST /api/push/subscribe` stores subscription per user | P0-035.2 | Endpoint returns 200; DB row created |
| P0-068.4 | Integration test: subscribe → endpoint → DB row exists | P0-068.3 | Test green |
| P0-069 | Bangla font (Noto Sans Bengali) — *deferred per [ADR-0003](./docs/adr/0003-defer-i18n.md)* | — | n/a (deferred) |
| P0-070.1 | Top-bar component (masthead with logo slot + user dropdown) | P0-065 | Renders on `/`; responsive |
| P0-070.2 | Bottom-nav for mobile (Home / Wall / Roster / Events / Chat / Settings) | P0-065 | Renders at viewport ≤768px; active route highlighted |
| P0-070.3 | Theme switcher (light / dark / system) wired to `.light` class | P0-065 | Toggling switcher changes theme; preference persists |
| P0-070.4 | Responsive layout test pages (phone / tablet / desktop) | P0-070.1..3 | Visual test passes on three breakpoints |
| P0-071.1 | Install `@tanstack/react-query`; set up `QueryClientProvider` | P0-039.2 | Provider context available |
| P0-071.2 | Auth-aware fetch wrapper that injects Clerk JWT into requests | P0-039.2, P0-071.1 | Sample query reaches API with `Authorization` header |
| P0-071.3 | Sample authenticated query renders data from `/api/whoami` | P0-071.2 | Authenticated page shows user info |
| P0-072 | Zustand store for client-only state | P0-064 | Sample slice round-trips state across components |
| P0-073.1 | Install `@sentry/nextjs`; configure DSN from env | P0-008 | App boots without errors |
| P0-073.2 | Test error in dev appears in Sentry within 1 min | P0-073.1 | Sentry inbox shows captured event |
| P0-074 | PostHog SDK installed + page-view tracking | P0-010 | Page views captured in PostHog within 1 min |
| P0-075 | Flagsmith SDK installed + sample flag toggle | P0-012 | Sample flag visible in dashboard; SDK reflects toggle |
| P0-076.1 | Cookie consent banner component + cookie state persistence | P0-064 | Banner appears on first visit; choice persists |
| P0-076.2 | `docs/cookie-policy.md` stub | P0-076.1 | Policy linked from banner + footer |
| P0-077.1 | Next.js middleware extracts subdomain from host | P0-064 | Middleware reads `<slug>.localhost` correctly |
| P0-077.2 | Pass tenant context to pages via React Context | P0-077.1 | Page reads tenant from context |
| P0-077.3 | Dev hosts file note in README; `<slug>.localhost:3000` resolves to app | P0-077.1 | Documented + works after hosts file edit |

### CI/CD

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-078.1 | GitHub Actions workflow: `dotnet build` + `dotnet test --filter Category!=Integration` on PR | P0-020.10 | Required check on `main`; passes on clean branch |
| P0-078.2 | GitHub Actions workflow: `npm run lint` + `npm run build` for `apps/web` on PR | P0-064 | Required check on `main`; passes |
| P0-079 | Integration tests against ephemeral Postgres in CI (Testcontainers) | P0-033.4, P0-078.1 | Integration suite runs on PR; green |
| P0-080 | Dependabot config for NuGet + npm | P0-014.1 | Dependabot opens PR weekly when updates exist |
| P0-081 | GitHub secret scanning + push protection enabled | P0-014.1 | PR with planted secret fails check |
| P0-082 | CodeQL workflow for C# + TypeScript | P0-078.1 | Security findings reported on PRs |
| P0-083 | SBOM (CycloneDX) generated on release | P0-078.1 | SBOM artifact attached to GitHub release |
| P0-084 | GitHub Actions: deploy backend to dev on `main` merge | P0-059.3, P0-078.1 | Merging no-op PR triggers dev deploy + new commit hash served |
| P0-085 | GitHub Actions: deploy frontend to Vercel preview on PR | P0-064 | Each PR comment shows preview URL |
| P0-086 | GitHub Actions: deploy to staging on tag (`v*-staging`) | P0-060, P0-084 | Tag triggers staging deploy |
| P0-087 | GitHub Actions: deploy to prod on **manual approval** in environment | P0-061, P0-086 | Manual gate enforced |
| P0-088 | Migrations runner job (separate from app deploy) | P0-033.4, P0-084 | Migrations apply before app rolls |

### Observability + ops

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-089.1 | Install OpenTelemetry packages; configure exporter to Azure Monitor | P0-057.4 | Traces appear in App Insights |
| P0-089.2 | Enrich spans with `tenant_id` + `user_id` from `ITenantContext` | P0-030.1, P0-089.1 | Traces filterable by tenant in App Insights |
| P0-090 | Serilog structured logging with `tenant_id` enricher | P0-030.1 | Logs queryable per tenant in Log Analytics |
| P0-091 | App Insights dashboard for key metrics (RPS, p95 latency, errors, DB calls) | P0-089.2 | Dashboard pinned in Azure portal |
| P0-092 | Better Stack uptime checks → `/health` on dev/staging/prod | P0-026.1 | Monitors green; alerts wired to email |
| P0-093 | Better Stack public status page at `status.earlyeagles.club` | P0-009 | Status page resolves |
| P0-094 | Error-budget + alerting thresholds doc in `docs/runbooks/error-budgets.md` | P0-091 | Doc reviewed; thresholds defined |

### Documentation

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-095 | README: project overview + quick start (local dev) | P0-064, P0-020.10 | New dev can run `npm run dev` + `dotnet run` from instructions alone |
| P0-096 | Architecture diagram (mermaid) embedded in README | — | Diagram renders on GitHub |
| P0-097.1 | Runbook: `docs/runbooks/deploy.md` | P0-087 | Doc covers dev/staging/prod deploy flow |
| P0-097.2 | Runbook: `docs/runbooks/rollback.md` | P0-087 | Doc covers GH Actions rollback + DB compensation |
| P0-097.3 | Runbook: `docs/runbooks/db-migrate.md` | P0-088 | Doc covers safe migration patterns |
| P0-097.4 | Runbook: `docs/runbooks/incident-response.md` | P0-091, P0-092 | Doc covers paging, escalation, comms |
| P0-098 | Contribution guide: branching, commit format, PR template | P0-014.6 | `CONTRIBUTING.md` complete with examples |
| P0-099 | Glossary of domain terms (`docs/glossary.md`) | — | Tenant, Member, Session, RSVP, Attendance, Dues defined |

### Phase 0 acceptance criteria

Phase 0 is **done** when:

- [ ] Dev, staging, and prod environments are all provisioned and healthy via Bicep.
- [ ] A new pull request runs build + unit + integration + isolation tests, and on merge auto-deploys to dev.
- [ ] **Tenant isolation integration test (P0-034.*) passes in CI as a required check.**
- [ ] An authenticated user can sign in via Clerk in all three environments.
- [ ] A test Stripe webhook updates a tenant's plan locally.
- [ ] A test email sends end-to-end via Resend (English only — Bangla deferred per ADR-0003).
- [ ] App Insights, Sentry, PostHog, Better Stack, and Flagsmith are all collecting events from at least one environment.
- [ ] Status page resolves at `status.earlyeagles.club`.
- [ ] All runbooks (P0-097.*) exist and the owner has done a successful manual deploy of each environment.

---

## Phase 1 — Attendance core (Tenant 0 only)

**Goal:** Early Eagles uses the platform every Sunday. No public signup; Tenant 0 is hard-coded for now. All features must be tenant-scoped under the hood — Phase 2 flips public signup on.

**Estimated effort:** ~30 tasks · several months at any sustainable pace.

### Domain + data

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P1-001 | Domain entities: `Member`, `Session`, `Attendance`, `Payment`, `Announcement`, `Comment` (all tenant-scoped) | M | P0-021 | Unit tests cover invariants |
| P1-002 | EF Core migration adding the above tables | S | P1-001 | Migration applies cleanly |
| P1-003 | `Member` syncs from Clerk org membership webhooks | M | P0-036 | New Clerk member appears as `Member` |
| P1-004 | Tenant 0 seed: Early Eagles members + first Sunday session | M | P1-002 | Dev DB has Tenant 0 ready |

### API endpoints

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P1-005 | `GET /sessions?week=YYYY-WW` — current/given week sessions for caller's tenant | S | P1-001 | Returns tenant-scoped sessions |
| P1-006 | `POST /sessions` (admin) — create session | S | P1-001 | Tenant-admin creates session |
| P1-007 | `PUT /sessions/{id}/rsvp` — RSVP yes/no/maybe | S | P1-001 | Member toggles RSVP |
| P1-008 | `POST /sessions/{id}/payment-report` — member self-reports Zelle payment (sender + ref) | S | P1-001 | Status moves to `reported` |
| P1-009 | `POST /sessions/{id}/payment-confirm` (admin) — confirm payment | S | P1-008 | Status moves to `confirmed`; raises `PaymentConfirmed` event |
| P1-010 | `PaymentConfirmed` handler: send email to admin group via Resend (en + bn template) | M | P0-053, P1-009 | Email arrives |
| P1-011 | `GET /leaderboard?period=year` — sessions attended, times paid, last played | M | P1-001 | Sorted list returned |
| P1-012 | `POST /announcements` (admin) + `POST /announcements/{id}/comments` | S | P1-001 | Endpoints work |
| P1-013 | `GET /announcements?limit=20` | S | P1-001 | Paginated feed |

### Frontend pages

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P1-014 | "This Sunday" home card: session detail + RSVP buttons | M | P0-070, P1-005, P1-007 | Member can RSVP |
| P1-015 | Week navigator component: arrows, "Wednesday, January 1, 2025" format, future-day arrow greyed at today | M | P0-066, P1-005 | Navigation correct in en + bn |
| P1-016 | Payment self-report form (sender name + Zelle reference) | S | P1-008 | Form validates and submits |
| P1-017 | Admin payment-confirm UI (toggle per member per session) | M | P1-009 | Admin confirms in one click |
| P1-018 | Member roster page (admin sees all, members see own org) | M | P1-001 | Roster renders |
| P1-019 | Leaderboard page: sortable table with three metrics | M | P1-011 | Sort + filter work |
| P1-020 | Announcements feed + create + comment | M | P1-012, P1-013 | Posting + commenting work |
| P1-021 | Profile page: name, Bangla name, language preference, avatar | M | P1-001 | Edits persist |

### i18n + UX polish

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P1-022 | Bangla translation pass for all Phase 1 strings | M | P0-066 | Native speaker reviews |
| P1-023 | PWA install prompt UX (custom card, not raw browser prompt) | S | P0-067 | Prompt appears once per device |
| P1-024 | Mobile responsive QA pass on all Phase 1 pages | M | P1-014..P1-021 | Looks correct on iPhone SE width |
| P1-025 | Empty states + skeletons for slow networks | S | P1-014..P1-021 | No raw spinners visible |
| P1-026 | Web Push: subscribe → notify admin on `payment.reported` | M | P0-068, P1-008 | Push lands on admin device |

### Tests + acceptance

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P1-027 | Unit tests for Domain invariants (RSVP rules, payment state machine) | M | P1-001 | Coverage > 80% on Domain |
| P1-028 | Integration tests for each command/query handler | L | P1-005..P1-013 | All handlers covered |
| P1-029 | Playwright E2E: sign-in → RSVP → pay → admin confirm → leaderboard updates | L | P1-014..P1-021 | Test green in CI |
| P1-030 | Tenant 0 owner dry-run on staging — first real session | M | P1-029 | Owner uses it for one Sunday |

### Phase 1 acceptance criteria

- [ ] Early Eagles uses the system for at least 2 consecutive Sundays without falling back to WhatsApp for attendance/payment.
- [ ] All Phase 1 endpoints have tenant isolation tests.
- [ ] Bangla and English UIs are both reviewed by a native Bangla speaker.
- [ ] Mobile usability passes on a real phone (not just devtools simulation).
- [ ] Audit log records every payment confirmation, member edit, and announcement.

---

## Phase 2 — SaaS commercialization

**Goal:** Any badminton club can sign up, pay, and use the platform within 10 minutes. White-label so each tenant feels like its own product.

**Estimated effort:** ~25 tasks · several months at any sustainable pace.

### Public-facing surface

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P2-001 | Public landing page (`earlyeagles.club` root): hero, features, pricing, sign-up CTA | M | P0-064 | Page live |
| P2-002 | Pricing page reflecting Free/Pro/Enterprise tiers | S | P2-001 | Stripe price IDs wired |
| P2-003 | Demo screenshots / short video of the product | M | P1-029 | Embedded on landing page |
| P2-004 | ToS, Privacy Policy, DPA, Cookie Policy pages | M | — | Reviewed by lawyer (or Termly) |
| P2-005 | Marketing analytics (PostHog page-views + conversion funnel) | S | P0-074 | Funnel visible |

### Tenant onboarding

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P2-006 | `POST /signup` flow: email + club name → Clerk Organization create | M | P0-035 | Org created |
| P2-007 | Stripe Checkout redirect for plan selection | M | P0-041 | Stripe Hosted Checkout works |
| P2-008 | `checkout.session.completed` webhook → tenant provisioning | M | P0-043 | Tenant row + slug + initial admin created |
| P2-009 | Tenant onboarding wizard: locale, timezone, branding (logo, primary color), first session | L | P2-008 | Wizard completes in <5 min |
| P2-010 | Subdomain assignment: tenant chooses slug; reserved-slug list enforced | M | P0-031 | Slug uniqueness enforced |

### White-label + tenant settings

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P2-011 | Branding settings UI: logo upload (R2), primary color, secondary color | M | P0-013 | Theme applies on tenant subdomain |
| P2-012 | Theme renderer: CSS variables driven by tenant config | M | P2-011 | Theme switches per tenant |
| P2-013 | Custom subdomain: `<slug>.earlyeagles.club` with auto SSL via Cloudflare | M | P0-002 | Cert provisioned automatically |
| P2-014 | Custom domain support: tenant adds `badminton.example.com` → CNAME instructions + SSL | L | P2-013 | Custom domain works end-to-end |
| P2-015 | Per-tenant copy customization (welcome email, RSVP labels) | M | P0-054 | Templates editable |

### Admin consoles

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P2-016 | Tenant-admin console: members, settings, branding, audit search, billing | L | P0-048, P2-011 | All features accessible |
| P2-017 | Stripe Customer Portal embed (manage subscription, view invoices) | S | P0-041 | Portal opens correctly |
| P2-018 | Plan limits enforcement (Free: 1 club, ≤25 members; Pro: unlimited) | M | P0-045 | Adding 26th member on Free blocked |
| P2-019 | Plan upgrade / downgrade flow | M | P0-045 | Switching tier works |
| P2-020 | Dunning + suspension on failed payment (`invoice.payment_failed` → email → grace period → freeze) | L | P0-043 | Tenant frozen after grace period |
| P2-021 | Platform-admin console: list tenants, impersonate (for support), force resets | L | P0-035 | Operator can support customers |
| P2-022 | Tenant deactivation / deletion (soft delete + GDPR-style data export) | L | P2-021 | Compliant with privacy policy |

### Quality + go-live

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P2-023 | E2E test: signup → checkout → onboarding → invite member → first session | L | P2-006..P2-010 | Test green in CI |
| P2-024 | Load test: 50 concurrent tenants, 500 concurrent users | M | P2-023 | p95 latency < 500ms |
| P2-025 | Beta launch with 2–3 friendly clubs | L | P2-023 | Feedback collected; bugs filed |

### Phase 2 acceptance criteria

- [ ] A new club can sign up, pay, and run their first session within one calendar day.
- [ ] At least one paying tenant other than Early Eagles is using the platform.
- [ ] Stripe webhooks are reliable (no missed billing events for 30 days straight).
- [ ] White-label custom subdomain works for at least 2 tenants.
- [ ] Platform-admin can impersonate users to debug support tickets.
- [ ] Public status page reflects current uptime accurately.

---

## Phase 3 — Real-time chat

**Goal:** Replace tenant reliance on WhatsApp for in-app communication.

**Estimated effort:** ~19 tasks (15 original + 4 reactions implementation per [ADR-0005](./docs/adr/0005-light-social-layer.md)).

| ID | Task | Effort | Acceptance |
|---|---|---|---|
| P3-001 | SignalR hub + Azure SignalR Service binding | M | Connection works |
| P3-002 | `Channel`, `Message`, `MessageRead`, `Reaction` entities | M | Schema migration |
| P3-003 | Group channel auto-created per tenant (e.g., `general`) | S | Default channel exists |
| P3-004 | DM channel creation between members | M | DMs work |
| P3-005 | Message send + receive over SignalR | M | Real-time messages |
| P3-006 | Image attachments via R2 (presigned upload) | M | Images render |
| P3-007 | Typing indicators + presence | M | Indicators work |
| P3-008 | Read receipts | S | Receipts tracked |
| P3-009 | Web Push for mentions + DMs | M | Push works |
| P3-010 | Mute / unmute per channel | S | Settings persist |
| P3-011 | Search within channel (Postgres FTS) | M | Search works |
| P3-012 | Moderation: admin can delete messages | S | Audit log entry created |
| P3-013 | Mobile-friendly chat UI | M | Usable on phone |
| P3-014 | Bangla translations *(deferred per [ADR-0003](./docs/adr/0003-defer-i18n.md))* | S | Strings translated |
| P3-015 | E2E + load tests for chat | L | Tests green |
| P3-016 | Reactions API endpoints (add / remove / list per message) | S | POST/DELETE/GET `/api/messages/{id}/reactions` work; idempotent per (user, emoji) |
| P3-017 | Reactions UI on chat messages — picker + inline aggregate display | M | Tap message → emoji picker; reactions render with counts below the message |
| P3-018 | Reaction aggregate denormalisation — counts cached on `Message` to avoid N+1 | S | Message list response includes aggregate counts in one query |
| P3-019 | Reaction component extracted as shared primitive for re-use in Phase 4 (photos + announcement comments) | S | One `<Reactions targetType targetId />` component used across surfaces |

---

## Phase 4 — Events + guests

**Goal:** Picnic, fishing, and tournament events; non-member guests can RSVP via email signup. Plus the **club photo wall** and **announcement comments** added per [ADR-0005](./docs/adr/0005-light-social-layer.md).

**Estimated effort:** ~32 tasks (20 original + 12 social-layer additions per ADR-0005).

| ID | Task | Acceptance |
|---|---|---|
| P4-001 | Generalized `Event` entity (type, capacity, fee, men-only flag, family flag) | Schema |
| P4-002 | Event types: weekly-session, picnic, fishing, tournament | Type switch works |
| P4-003 | Family RSVP fields (spouse + kid count) | UI captures counts |
| P4-004 | Men-only flag enforcement | Female members blocked from male-only |
| P4-005 | Tournament: round-robin generator | Brackets render |
| P4-006 | Tournament: single-elimination generator | Brackets render |
| P4-007 | Live tournament leaderboard | Updates in real time |
| P4-008 | Guest invite link generation | Link works |
| P4-009 | Guest email signup → guest role | Guest signs in |
| P4-010 | Guest RSVP + payment self-report | Guest contributes |
| P4-011 | Event photo gallery (R2) | Photos upload |
| P4-012 | Event cover image | Cover renders |
| P4-013 | Event reminders (email + push) | Reminders sent |
| P4-014 | Calendar integration (.ics export per event) | iCal works |
| P4-015 | Cross-event leaderboard (most active across all events) | Stats correct |
| P4-016 | Event categories per tenant (configurable) | Categories editable |
| P4-017 | Per-tenant event archetype templates | Quick-create works |
| P4-018 | Permissions: who can create which event type | RBAC enforced |
| P4-019 | Bangla translations | Strings translated *(deferred per [ADR-0003](./docs/adr/0003-defer-i18n.md))* |
| P4-020 | E2E tests | Tests green |

### Light social layer (per [ADR-0005](./docs/adr/0005-light-social-layer.md))

Club photo wall, reactions on photos / announcements, and announcement comments. Land after the event domain ships so the underlying `Photo` entity and notification plumbing are shared.

| ID | Task | Acceptance |
|---|---|---|
| P4-021 | Generalise `Photo` entity: nullable `event_id`, plus `tenant_id`, `uploader_id`, optional `caption` | Schema migration; photo with NULL event_id appears only in wall, photo with event_id appears in both wall and event gallery |
| P4-022 | Photo wall feed API — paginated, chronological, club-scoped, optional event filter | `GET /api/photos?cursor=…&event=…` returns 20 most-recent first; cursor-based pagination |
| P4-023 | Photo wall feed UI — responsive grid, infinite scroll, thumbnail-first loading | Member sees grid on `/wall`; scrolls to load older; lazy-loads images |
| P4-024 | Photo upload UX from any surface (home card, event page, chat attach) — single `<PhotoUploader>` component | Drop or pick photo; presigned R2 upload; result appears in feed without page reload |
| P4-025 | Photo detail view — modal/page with full image, uploader, caption, reactions, delete affordance | Tap thumb → detail; admin sees delete button; uploader sees delete button on own photos |
| P4-026 | Reactions on photos (uses P3-019 shared primitive) | Same emoji set as chat; counts aggregate; tap to react |
| P4-027 | `AnnouncementComment` entity — single flat thread per announcement, no nesting | Schema migration; one parent (announcement_id) per comment |
| P4-028 | Announcement comment API endpoints — list, create, delete (admin or author) | `GET/POST/DELETE /api/announcements/{id}/comments` work with auth gates |
| P4-029 | Announcement comment UI on announcement detail — list + composer + count badge in feed | Member sees comments; can post; admin / author can delete |
| P4-030 | Notifications: new photo posted (opt-in), new announcement (opt-out), new comment on announcement you commented on (opt-out) | Web Push fires per rule; settings UI exposes opt-in/opt-out toggles |
| P4-031 | Admin moderation — delete photo / comment / reaction; audit-logged | Admin action writes `audit_events` row with reason; soft delete with reversible window |
| P4-032 | Photo wall E2E test + image-upload load test | Tests green; can sustain 20 concurrent uploads without R2 errors |

---

## Phase 5 — AI Coach

**Goal:** Embedded badminton coach AI (English + Bangla humor) — differentiation feature.

**Estimated effort:** ~10 tasks.

| ID | Task |
|---|---|
| P5-001 | AI provider abstraction (OpenAI + Anthropic adapters) |
| P5-002 | Coach prompt + persona library (en + bn humor) |
| P5-003 | `/coach` chat endpoint + streaming response |
| P5-004 | Per-tenant config (enable, persona tone, language preference) |
| P5-005 | Usage metering + per-tier limits (Free: 0, Pro: 50/mo, Enterprise: unlimited) |
| P5-006 | Drill plan generator (structured response) |
| P5-007 | Knowledge base ingestion (BWF rules, badminton bible excerpts) — RAG |
| P5-008 | Cost tracking + alerting per tenant |
| P5-009 | Frontend chat UI for Coach |
| P5-010 | Bangla translations + safety review |

---

## Phase 6 — Mobile app

**Goal:** Native iOS + Android apps reusing the existing API.

**Estimated effort:** ~30 tasks (a whole new client app).

Major task groups:

- Framework decision: Flutter vs Expo (write ADR-00X)
- Project scaffolding + CI for mobile
- Authentication via Clerk SDK (Flutter or Expo)
- Tenant subdomain handling on mobile
- Core screens: this-Sunday, RSVP, payment, leaderboard, announcements, profile
- Chat in mobile (shared SignalR or separate WebSocket impl)
- FCM push notifications
- Offline-first data layer
- App Store + Play Store listings, screenshots, descriptions
- Beta testing via TestFlight + Play Console internal track
- App Store / Play Store review submissions

Re-plan in detail when Phase 6 begins.

---

## Phase 7 — Enterprise tier

**Goal:** Sellable to larger orgs / federations / clubs requiring SSO and audit.

**Estimated effort:** ~15 tasks.

| ID | Task |
|---|---|
| P7-001 | Clerk Pro plan + B2B SSO (SAML/OIDC) configuration UI |
| P7-002 | SCIM user provisioning |
| P7-003 | Advanced audit log search + export (CSV/JSON) |
| P7-004 | Per-tenant retention policies for audit logs |
| P7-005 | SLA monitoring + reporting (uptime, response time per tenant) |
| P7-006 | Dedicated database option (per-tenant DB provisioning via Bicep) |
| P7-007 | Region-residency option (deploy stack to EU region for EU tenants) |
| P7-008 | Custom contract terms + manual invoicing path |
| P7-009 | Enterprise onboarding workflow (white-glove) |
| P7-010 | Security compliance docs (SOC 2 readiness, DPA addenda) |
| P7-011 | Vulnerability disclosure program (security.txt + reward policy) |
| P7-012 | Penetration test (external) |
| P7-013 | IP allow-listing per tenant |
| P7-014 | Audit-log SIEM forwarding (Splunk, Datadog) |
| P7-015 | Customer success onboarding playbook |

---

## Phase 8 — Microsoft 365 / Teams Agent (optional)

**Goal:** Optional surface for Teams-native customers.

**Estimated effort:** ~10 tasks.

| ID | Task |
|---|---|
| P8-001 | Teams app manifest + Bot Framework registration |
| P8-002 | Declarative Copilot agent definition (TypeSpec) |
| P8-003 | OIDC handshake: Teams identity → Clerk session |
| P8-004 | Card formatting for: this-Sunday, RSVP, payment status, leaderboard |
| P8-005 | Teams channel announcements posting |
| P8-006 | Microsoft 365 Marketplace listing |
| P8-007 | Per-tenant Teams subscription enable/disable |
| P8-008 | Teams admin documentation |
| P8-009 | Compliance review for Microsoft Marketplace |
| P8-010 | Beta with one Teams-using customer |

Decide whether to do Phase 8 only after Phase 7 is stable and at least one prospect requires Teams.

---

## Phase 9 — XR / MatchCourt (long-horizon)

**Goal:** Unity + Meta XR virtual clubhouse and remote rally simulation.

**Estimated effort:** Years. Not planned in detail until at least 50 paying tenants exist.

Sketched task groups:

- Unity project + Meta XR SDK
- OIDC auth against Clerk in Unity
- Virtual lobby + avatar
- Voice chat (WebRTC or Photon Voice)
- Rally simulation (motion tracking, physics)
- Ghost replay mode
- AI coach overlay
- Mobile AR drills (separate Phase 9a?)
- Sponsorship + funding model (this is real R&D money)
- Beta program with Tenant 0

Re-plan from scratch when seriously starting.

---

## Appendix — tasks split after the original plan

Per the conventions above, splits are **appended** here rather than rewritten in place. The
original row stays where it is; the sub-IDs below are what actually gets built and verified.

### P0-050.2 split (2026-09-21)

`P0-050.2` bundled two independent concerns — the drain's publish/mark-processed pipeline, and
the Azure Service Bus transport it publishes through. The first needs nothing but Postgres; the
second is blocked behind `P0-051`, which is itself blocked behind the Bicep work in `P0-058.4`.
Keeping them together would have left a shippable, testable piece of work parked behind cloud
provisioning for no reason.

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-050.2.1 | Drain publishes each pending row through an `IOutboxPublisher` port and stamps the outcome (`ProcessedAt` on success, `AttemptCount` + `LastError` on failure) | P0-050.1 | Integration test: published row is stamped and leaves the queue; failed row stays pending with its attempt counted |
| P0-050.2.2 | `ServiceBusOutboxPublisher` — real Azure Service Bus implementation of the port, replacing the `LoggingOutboxPublisher` placeholder | P0-050.2.1, P0-051 | Outbox row appears as a message on the topic |

`P0-050.3` (emulator/testcontainer integration test) now depends on `P0-050.2.2`, not on
`P0-050.2` as a whole.

### Container registry added (2026-09-28)

Found while building `P0-058.3`: nothing in the original plan creates a container registry,
yet the Container App can only run a publicly pullable placeholder image until one exists.
[`infra-conventions.md`](./docs/infra-conventions.md) already reserves the `cr` abbreviation
("one per subscription, no env"), so the gap is in the task list, not the design.

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-058.6 | `infra/modules/containerRegistry.bicep` (Basic SKU, admin user disabled, anonymous pull off) | P0-057.1 | `bicep build` succeeds; after deploy, `az acr show` returns the registry |
| P0-062.4 | Grant the Container App's managed identity `AcrPull` on the registry; point `containerApp.bicep` at it | P0-062.1, P0-058.6 | Container App pulls a private image with no registry password |

Open for `P0-058.6` to settle: conventions §3.2 says one registry per subscription with no
environment segment, but its example name (`creedeveus`) contains `dev`. Decide which is right
(and where a subscription-wide registry is deployed from, since every `main.<env>.bicep` is
per-environment), then fix §3.2 in the same change. `P0-084` (deploy backend to dev) now also
depends on `P0-062.4`.

**Settled 2026-10-06 (P0-058.6):** one registry shared by all environments,
`creesharedeus<unique>` in a new `rg-ee-shared-eus`, with `shared` added as a fourth
environment code. It is deployed by its own entry point, so one task is added:

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-059.4 | `infra/main.shared.bicep` + `main.shared.bicepparam`: `rg-ee-shared-eus` and the registry | P0-057.1, P0-058.6 | `bicep build` succeeds; after deploy, `az acr show` returns the registry |

`P0-062.4` now depends on `P0-059.4` as well.

### Redis product change (2026-10-06)

`P0-058.5` was planned as "Standard SKU, network ACLs" on Azure Cache for Redis. That product
stopped accepting new instances on 2026-10-01, so the task was delivered on **Azure Managed
Redis** instead — see [ADR-0008](./docs/adr/0008-azure-managed-redis.md). Changes that follow:

- `P0-058.5` verify becomes: after deploy, `redis-cli -h <host> -p 10000 --tls --user <objectId> --pass <entra token> ping` returns `PONG` (carried to `P0-059.3`).
- `P0-062.3` also covers Redis: an `accessPolicyAssignments` entry on the cache's `default` database for the API's managed identity (not an Azure RBAC role).
- There are no "network ACLs" to set in Phase 0: Azure Managed Redis has no IP firewall, only public/private access. Public + Entra-only is the Phase 0 position; private endpoints stay Phase 7 (conventions §9).

### P0-060 split (2026-10-06)

`P0-060` bundled two things: a refactor (stop `main.dev.bicep` being the only place the
environment wiring lives) and a new environment. Split so the refactor can be proven to change
nothing before staging is built on it.

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-060.1 | Extract the wiring into `infra/environment.bicep`; `main.dev.bicep` passes env code + §7 sizing | P0-059.1 | Compiled dev template equivalent to before (module templates identical, parameters equal) |
| P0-060.2 | `infra/main.staging.bicep` + `main.staging.bicepparam` | P0-060.1 | `bicep build` / `build-params` succeed; staging deploy carried to the first real deploy |

`P0-061` (`main.prod.bicep`) builds on `P0-060.1` the same way.

### Local dev instead of cloud dev (2026-10-06)

[ADR-0009](./docs/adr/0009-local-dev-no-cloud-dev-environment.md): dev runs on the developer's
machine; prod is the only standing Azure environment, staging is on demand. Task changes:

| ID | Change |
|---|---|
| **P0-059.5** (new) | `docker-compose.yml`: Postgres 17 + Azure Service Bus emulator (and the SQL container it needs), emulator `Config.json` declaring the outbox topic(s). Verify: `docker compose up -d` → API connects to Postgres; emulator answers on its AMQP port. |
| P0-059.3 | Now only the sandbox smoke test: deploy `main.dev.bicep` with `EE_SANDBOX=true` (the parameter file's new default), verify healthy, delete the RG. |
| P0-051 | Topics/subscriptions are declared in the emulator config (P0-059.5) first; in prod's `serviceBusTopics` when prod gets Service Bus (P0-087). |
| P0-084 | Was "deploy backend to dev on `main` merge" → **build, test and push the image to the shared registry on `main` merge; no deploy.** Depends on P0-059.4 instead of P0-059.3. |
| P0-086 | Staging deploy on `v*-staging` tag stays, but staging is torn down afterwards (on-demand). |

### Registry moves to GHCR (2026-10-06)

[ADR-0010](./docs/adr/0010-ghcr-instead-of-acr.md): images are published to
`ghcr.io/razibur/early-eagles-api` instead of an Azure Container Registry. `P0-058.6` and
`P0-059.4` stay marked done but their files are removed (recoverable from git). Task changes:

| ID | Change |
|---|---|
| P0-062.4 | **Redefined:** store a GitHub token scoped to `read:packages` in Key Vault; give `containerApp.bicep` an optional private-registry block (`server: ghcr.io`, username, password secret as a Key Vault reference via the managed identity). Verify: the Container App pulls the private image. Depends on P0-062.1, P0-062.3. |
| P0-084 | Pushes to `ghcr.io` with `GITHUB_TOKEN` (`packages: write`); no Azure login needed to publish. No longer depends on P0-059.4. |
| P0-063 | The infra runbook adds rotating the `read:packages` token before it expires. |

### P0-062.2 split (2026-10-23)

"Grant the managed identity access to Postgres" is three jobs in three places, none of which
Bicep alone can do: the API must sign in with a token instead of a password, the identity needs
a role *inside* Postgres (data-plane SQL that ARM can't run), and the deployed app needs the
connection settings. Split so each is verifiable alone:

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-062.2.1 | API connects with an Entra token when `Postgres:Authentication = Entra` (`NpgsqlDataSource` + periodic token provider over `TokenCredential`); password mode unchanged for local dev | P0-062.1 | Unit tests: Entra mode fetches the `ossrdbms-aad` token, refuses a password / missing username; password mode creates no credential |
| P0-062.2.2 | Versioned SQL script creating the identity's Postgres role (`pgaadauth_create_principal_with_oid`) with least-privilege grants (CONNECT, schema USAGE, table DML, default privileges for future tables) — no DDL | P0-062.1, P0-058.1 | Grant part runs clean against local Postgres for a stand-in role; role can DML, cannot `CREATE TABLE` |
| P0-062.2.3 | Wire the app: `Postgres__Authentication=Entra` + a password-less connection string (`Username=id-ee-api-<env>-eus`, `SslMode=Require`) as Container App env vars | P0-062.2.1 | `az bicep build` clean; compiled env vars checked. Live "API connects without password" carried to P0-059.3 |

**Note (2026-10-25, P0-062.2.3):** shipped with `SslMode=VerifyFull`, not `Require`. In Npgsql,
`Require` encrypts but skips certificate validation. `VerifyFull` also checks the chain and host
name, and Azure Postgres certificates chain to public roots in the .NET image's trust store.

### P0-062.3 split (2026-10-26)

"Grant the identity access to Key Vault + Service Bus" (plus Redis, per the 2026-10-06 note)
is three grants on three resources, each deployed only where the resource exists. Each grant
lives in the module that owns the resource, as a list of principal IDs, and `environment.bicep`
passes the API identity:

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-062.3.1 | Key Vault Secrets User on the vault (`keyVault.bicep` `secretsUserPrincipalIds`) | P0-062.1, P0-057.2 | `az bicep build` clean; compiled assignment checked. Live "API reads a secret" carried to P0-059.3 |
| P0-062.3.2 | Service Bus Data Sender + Data Receiver on the namespace (`serviceBus.bicep`): the API publishes (outbox) and consumes (P0-052) | P0-062.1, P0-058.4 | Same; only where Service Bus is deployed. Live "API publishes a message" carried to P0-059.3 |
| P0-062.3.3 | Redis `default` access-policy assignment on the database (`redis.bicep`), not an RBAC role | P0-062.1, P0-058.5 | Same; only where Redis is deployed (none today) |

### PWA without a plugin (2026-10-26)

[ADR-0011](./docs/adr/0011-hand-written-service-worker-instead-of-next-pwa.md): `next-pwa` is
unmaintained (last release 2022) and webpack-only, and Next 16 builds with Turbopack. The
service worker is hand-written instead, per the Next.js 16 PWA guide. Acceptance criteria are
unchanged:

| ID | Change |
|---|---|
| P0-067.1 | **Redefined:** hand-written `apps/web/public/sw.js` (no caching, no `fetch` handler), registered by a Client Component in the root layout; `next.config.ts` serves `/sw.js` with `no-cache` and a `self`-only CSP. Verify unchanged: service worker registers in dev. |
| P0-067.2 | **Reworded:** the manifest is the built-in `app/manifest.ts` metadata route, not `public/manifest.webmanifest`. |
| P0-067.4 | **Added:** fix the three Recharts 3.8 type errors in the sample charts (`components/charts/*`, from P0-019/064/065) that make `npm run build` fail. Verify: `npm run build` passes. Found while verifying P0-067.1. |
| P0-068.2 | Adds the `push` / `notificationclick` handlers to the same `public/sw.js`. |


### P0-026.2 split (2026-10-27)

"`/ready` returns 200 when DB + Service Bus reachable" is two checks with different blockers: the
database exists locally today, the Service Bus topic doesn't (P0-051). Split so the database half
ships now:

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-026.2.1 | `PostgresHealthCheck` (`SELECT 1` through the app's `NpgsqlDataSource`) registered behind `/ready` with a 5 s timeout; fixed-text descriptions because `/ready` is unauthenticated | P0-026.1, P0-062.2.1 | Integration: reachable DB → 200, unreachable → 503 within the timeout; live toggle of the compose Postgres flips it |
| P0-026.2.2 | Service Bus readiness check behind `/ready` | P0-050.2.2 | Integration against the emulator: topic reachable → 200, emulator stopped → 503 |

### P0-048 split (2026-10-27)

"Audit query endpoint (tenant-admin scope only) with pagination" has two halves with different
blockers: the query and paging need nothing new; the admin restriction needs Clerk role claims
(P0-038). Split so the query ships now, kept out of Production until it is restricted:

| ID | Task | Depends on | Verify |
|---|---|---|---|
| P0-048.1 | `GET /audit-events` via a Mediator `IQuery` + `IAuditLogReader` port; keyset paging on `(occurred_at, id)`; mapped outside Production only | P0-046.1, P0-032.2 | Integration: pages without gaps/duplicates, tenant-isolated, bad input → 400, Production → 404 |
| P0-048.2 | Tenant-admin authorization policy on the endpoint; map it in every environment | P0-048.1, P0-038 | Tenant-admin can list own audit; member → 403 |

### Logging without Serilog (2026-10-27)

`P0-090` named Serilog. No ADR or stack table picks it, and since P0-089.1 the OpenTelemetry pipeline
(Azure Monitor distro / OTLP) already exports `ILogger` logs. A second framework would add a sink to
keep in step without adding a capability. Reworded:

| ID | Change |
|---|---|
| P0-090 | **Reworded:** structured logging with tenant context on .NET's built-in `ILogger`: a request logging scope (`TenantId`, `TenantSlug`, `UserId`) from `TelemetryEnrichmentMiddleware`, JSON console output with scopes outside Development (stdout → Log Analytics), `IncludeScopes` on the OpenTelemetry log exporter. Verify unchanged: logs queryable per tenant in Log Analytics. |
---

## Sequencing notes

- **Backend solution skeleton (P0-020..P0-027) and frontend foundations (P0-064..P0-077) can run in parallel.** They meet at P0-039 (Clerk wiring) and P0-071 (auth fetch client).
- **Bicep IaC (P0-056..P0-063) must precede any cloud deploy.** Don't click anything in the portal.
- **Tenant isolation test (P0-034) is the gate for Phase 1.** If it fails, fix before adding any feature.
- **Stripe scaffolding (P0-041..P0-045) lands in Phase 0 even though the UI for it is Phase 2.** Wiring webhooks early avoids retrofitting.
- **Phase 1 must not introduce any non-tenant-scoped data.** Every new entity has `TenantId`.
- **Phase 2's white-label work (P2-011..P2-015) is the biggest design risk** — get it right, or every later visual change is painful.

## Tracking

This file is the **plan**, not the **tracker**. Use **GitHub Projects** (or Linear / Notion / whatever) for live status. Each task ID becomes one issue. When closing an issue, reference the task ID in the commit / PR message:

```
P0-034: tenant isolation integration test

Adds Tenant A vs Tenant B test that fails if EF query filter is bypassed.
```

When estimates turn out badly wrong, update the effort label here and write a brief note in the PR.

When tasks are added or split, **append** to this file and bump task IDs (don't rewrite history).

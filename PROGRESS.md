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

Last updated: 2026-05-24

---

## Phase 0 — Enterprise Foundations  ▰▰▱▱▱▱▱▱▱▱  ~12% (10 / ~80 tasks)

The non-negotiable platform foundation. **P0-034 (tenant-isolation integration
test) is the gate** — must stay green before any feature beyond Phase 0 ships.

### Pre-work — accounts, domain, secrets

External account / billing tasks. Mostly off the laptop; I can't verify these,
you confirm by logging into each console.

- `[ ]` **P0-001** Create Azure subscription + landing-zone resource groups (dev / staging / prod)
  ↳ verify: 3 RGs visible in Azure Portal
- `[ ]` **P0-002** Buy `earlyeagles.club` (or final domain) + configure DNS at provider
  ↳ verify: `nslookup earlyeagles.club` returns NS records
- `[ ]` **P0-003** Set up Azure Cost Management budget + alerts ($50/mo dev cap)
  ↳ verify: budget alert email arrives on test trigger
- `[ ]` **P0-004** Create GitHub org + repo `earlyeagles/early-eagles` (or rename current)
  ↳ verify: repo URL resolves with you as owner
- `[ ]` **P0-005** Create Stripe account (test mode) + verify business
  ↳ verify: Stripe dashboard accessible, test mode enabled
- `[ ]` **P0-006** Create Clerk account + first application
  ↳ verify: Clerk dashboard shows an app with publishable + secret keys
- `[ ]` **P0-007** Provision Cloudflare account + R2 bucket (test)
  ↳ verify: bucket lists in `wrangler r2 bucket list`
- `[ ]` **P0-008** Create Sentry account + project
  ↳ verify: Sentry DSN available
- `[ ]` **P0-009** Create Better Stack account + status page placeholder
  ↳ verify: status page URL resolves
- `[ ]` **P0-010** Create PostHog account + project
  ↳ verify: PostHog dashboard accessible
- `[ ]` **P0-011** Create Resend account + verify sending domain
  ↳ verify: send a test email from Resend dashboard
- `[ ]` **P0-012** Create Flagsmith account + environment
  ↳ verify: dashboard shows dev environment
- `[ ]` **P0-013** Set up password manager vault for shared secrets
  ↳ verify: vault entry created, can be unlocked

### Repository scaffolding

- `[x]` **P0-014** Repo skeleton — README, LICENSE, .gitignore, .editorconfig, CODE_OF_CONDUCT.md, CONTRIBUTING.md
  ↳ verify: `ls README.md LICENSE .gitignore .editorconfig` — all present. Still missing: CODE_OF_CONDUCT.md, CONTRIBUTING.md, CODEOWNERS
- `[ ]` **P0-015** Branch protection on `main`: require PR review, status checks, signed commits
  ↳ verify: GitHub Settings → Branches shows rule active, direct push to main fails
- `[ ]` **P0-016** Add `CODEOWNERS` file
  ↳ verify: open a test PR; you're auto-requested as reviewer
- `[ ]` **P0-017** ADR template at `docs/adr/0000-template.md`
  ↳ verify: file exists, contains the template scaffold
- `[x]` **P0-018** Move existing docs (Plan.md, project-scope.md, docs/) into repo
  ↳ verify: all referenced docs resolve under repo root
- `[x]` **P0-019** Monorepo layout: `apps/web`, `apps/api`, `infra`, `docs`, npm workspaces
  ↳ verify: `apps/web/` exists with package.json; root `package.json` declares `workspaces: ["apps/*"]`; `npm run dev` from root starts apps/web

### Backend — Clean Architecture solution

None started. `apps/api/` does not exist yet.

- `[ ]` **P0-020** `EarlyEagles.sln` with 7 projects: Api, Application, Domain, Infrastructure, Tests.Unit, Tests.Integration, Tests.Acceptance
  ↳ verify: `dotnet build apps/api/EarlyEagles.sln` → 0 errors; Domain has no reference to Infrastructure
- `[ ]` **P0-021** Base Domain types: `Entity<TId>`, `ValueObject`, `AggregateRoot`, `IDomainEvent`
  ↳ verify: `dotnet test apps/api/tests/EarlyEagles.Tests.Unit` passes; base-class tests green
- `[ ]` **P0-022** StyleCop + Roslyn analyzers + `.editorconfig` rules
  ↳ verify: `dotnet build` reports analyzer warnings on a deliberate violation
- `[ ]` **P0-023** MediatR + FluentValidation + AutoMapper wiring
  ↳ verify: sample command/query handler invokes from a test
- `[ ]` **P0-024** Pipeline behaviors: Logging, Validation, Transaction, Audit
  ↳ verify: each behavior unit-tested; logs visible for sample command
- `[ ]` **P0-025** Centralized exception middleware → RFC 7807 ProblemDetails
  ↳ verify: `curl /broken` returns `application/problem+json` with `title` + `status`
- `[ ]` **P0-026** Health endpoints `/health` (live) and `/ready` (DB + Service Bus)
  ↳ verify: `curl /health` → 200; `curl /ready` → 200 when deps up, 503 when down
- `[ ]` **P0-027** OpenAPI / Swagger with auth + tenant header documented
  ↳ verify: `/swagger` lists every endpoint with examples

### Multi-tenancy core

- `[ ]` **P0-028** `Tenant` aggregate (Id, Slug, DisplayName, Locale, Branding, Plan, Status)
  ↳ verify: unit tests for invariants pass
- `[ ]` **P0-029** `TenantMember` join entity
  ↳ verify: unit test for membership rules passes
- `[ ]` **P0-030** `ITenantContext` interface + scoped service
  ↳ verify: integration test confirms per-request tenant resolution
- `[ ]` **P0-031** Tenant-resolution middleware (subdomain → `TenantContext`)
  ↳ verify: hit `<slug>.localhost:5000/whoami`; response shows correct tenant
- `[ ]` **P0-032** EF Core `DbContext` with global query filter on every tenant-scoped entity
  ↳ verify: integration test issuing a query without tenant context returns empty set
- `[ ]` **P0-033** First EF Core migration: `tenants`, `tenant_members`, `audit_events`
  ↳ verify: `dotnet ef database update`; psql shows 3 tables with expected columns
- `[ ]` **P0-034** ★ **Tenant isolation integration test** — Tenant A cannot read Tenant B data via any endpoint
  ↳ verify: `dotnet test --filter Category=TenantIsolation` passes; CI fails if filter bypassed

### Identity — Clerk integration

- `[ ]` **P0-035** Clerk JWT validation middleware in Api
  ↳ verify: unauthenticated request to a protected route → 401; authenticated → 200
- `[ ]` **P0-036** Clerk webhook receiver (organization.created, organizationMembership.created, user.created, user.updated)
  ↳ verify: create org in Clerk dashboard → row appears in `tenants` table
- `[ ]` **P0-037** Svix webhook signature verification
  ↳ verify: webhook with bad signature → 400; good signature → 200
- `[ ]` **P0-038** Map Clerk org roles ↔ tenant-admin / member
  ↳ verify: admin endpoint requires `tenant-admin` role; regular user gets 403
- `[ ]` **P0-039** Frontend Clerk SDK setup in Next.js
  ↳ verify: `/sign-in` page loads; complete sign-in returns to app authenticated
- `[ ]` **P0-040** Sign-in methods: Google + email magic link + phone OTP + optional MFA
  ↳ verify: each method completes a sign-in in dev

### Billing — Stripe integration (scaffolding only)

- `[ ]` **P0-041** Stripe SDK setup (.NET + JS)
  ↳ verify: SDK initialised, can list products
- `[ ]` **P0-042** Stripe products + prices in dashboard (Free / Pro / Enterprise, test mode)
  ↳ verify: 3 products visible in Stripe dashboard with price IDs
- `[ ]` **P0-043** Stripe webhook receiver (checkout, subscription, invoice events)
  ↳ verify: trigger test webhook via Stripe CLI; handler logs the event
- `[ ]` **P0-044** Stripe webhook signature verification
  ↳ verify: webhook with bad signature → 400
- `[ ]` **P0-045** Map Stripe customer ↔ Tenant; subscription ↔ Plan
  ↳ verify: complete test checkout; `tenants.plan` updates

### Audit logging

- `[ ]` **P0-046** `AuditEvent` entity (append-only)
  ↳ verify: table exists; attempting UPDATE/DELETE in tests throws
- `[ ]` **P0-047** `IAuditService` + AuditBehavior MediatR pipeline
  ↳ verify: every state-changing command produces an audit row
- `[ ]` **P0-048** Audit query endpoint (tenant-admin scope)
  ↳ verify: tenant-admin can list own audit; regular user gets 403

### Domain events + Service Bus

- `[ ]` **P0-049** `IDomainEventDispatcher` + outbox in DbContext.SaveChangesAsync
  ↳ verify: integration test confirms event captured in same TX as state change
- `[ ]` **P0-050** Outbox processor background service publishing to Service Bus
  ↳ verify: event row in outbox table appears on Service Bus topic
- `[ ]` **P0-051** Service Bus topic + subscriptions in Bicep (depends on P0-058)
  ↳ verify: `az servicebus topic show` returns the topic
- `[ ]` **P0-052** First handler: `PaymentConfirmed` → log only
  ↳ verify: publish test event; handler log line appears

### Email + notifications scaffolding

- `[ ]` **P0-053** `IEmailSender` abstraction → Resend implementation
  ↳ verify: integration test sends to a test inbox via Resend
- `[-]` **P0-054** Email template rendering with i18n
  ↳ deferred per [ADR-0003](docs/adr/0003-defer-i18n.md). English-only templates only.
- `[ ]` **P0-055** Smoke-test email from dev → owner inbox
  ↳ verify: email arrives in your real inbox

### Infrastructure as Code (Bicep)

- `[ ]` **P0-056** Naming convention + tagging strategy doc (`docs/infra-conventions.md`)
  ↳ verify: doc exists; PR review enforces against it
- `[ ]` **P0-057** Bicep modules: resourceGroup, keyVault, logAnalytics, appInsights
  ↳ verify: `bicep build infra/modules/keyVault.bicep` succeeds
- `[ ]` **P0-058** Bicep modules: postgres-flexible, containerAppsEnv, containerApp, serviceBus, redis
  ↳ verify: each module deploys to a throwaway RG
- `[ ]` **P0-059** `infra/main.dev.bicep` orchestrating dev environment
  ↳ verify: `az deployment sub create` provisions full dev env in ≤10 min
- `[ ]` **P0-060** `infra/main.staging.bicep`
  ↳ verify: staging env deployed
- `[ ]` **P0-061** `infra/main.prod.bicep`
  ↳ verify: prod env deployed (empty, no traffic)
- `[ ]` **P0-062** Managed identity for Container App → Postgres + Key Vault + Service Bus
  ↳ verify: API in dev connects to Postgres without a password in config
- `[ ]` **P0-063** Deploy runbook at `docs/runbooks/deploy-infra.md`
  ↳ verify: doc exists with step-by-step commands

### Frontend foundations

- `[x]` **P0-064** `apps/web` Next.js 16 + TypeScript + ESLint + Prettier
  ↳ verify: `npm run dev` from repo root boots the app; page renders 200
- `[x]` **P0-065** Tailwind v4 + shadcn/ui (Base UI variant) + base components
  ↳ verify: `apps/web/components.json` exists; `apps/web/components/ui/{button,card,chart}.tsx` present
- `[-]` **P0-066** `next-intl` with locale routing; en + bn message files
  ↳ deferred per [ADR-0003](docs/adr/0003-defer-i18n.md)
- `[ ]` **P0-067** `next-pwa` + manifest + icons
  ↳ verify: Chrome DevTools → Application → Manifest shows valid manifest; install icon appears
- `[ ]` **P0-068** Web Push subscription scaffolding (VAPID keys, subscribe endpoint stub)
  ↳ verify: subscribing in browser writes a row to the DB
- `[-]` **P0-069** Bangla font (Noto Sans Bengali)
  ↳ deferred per [ADR-0003](docs/adr/0003-defer-i18n.md)
- `[~]` **P0-070** Layout shell + nav + theme switcher (locale switcher deferred per ADR-0003)
  ↳ partial: editorial layout exists at `/`; no nav, no theme switcher yet
  ↳ verify: nav links between pages; theme switcher toggles light/dark
- `[ ]` **P0-071** TanStack Query + auth-aware fetch client
  ↳ verify: sample authenticated API call from a page works
- `[ ]` **P0-072** Zustand store for client-only state
  ↳ verify: sample slice round-trips state on a page
- `[ ]` **P0-073** Sentry frontend integration
  ↳ verify: throw a test error; appears in Sentry within ~1 min
- `[ ]` **P0-074** PostHog SDK
  ↳ verify: page view captured in PostHog within ~1 min

### CI/CD

- `[ ]` **P0-075** GitHub Actions: PR lint + build + test (web + api)
  ↳ verify: open PR; checks run and pass on a clean branch
- `[ ]` **P0-076** GitHub Actions: deploy to dev on merge to `main`
  ↳ verify: merge a no-op PR; dev URL serves new commit hash
- `[ ]` **P0-077** GitHub Actions: deploy to staging (manual trigger)
  ↳ verify: workflow_dispatch produces a staging deploy
- `[ ]` **P0-078** GitHub Actions: deploy to prod (manual approval)
  ↳ verify: prod deploy requires environment approval
- `[ ]` **P0-079** SBOM generation + dependency scanning + secret scanning in CI
  ↳ verify: PR with a planted secret fails the check

### Observability + ops

- `[ ]` **P0-080** App Insights wired in API; traces tagged with `tenant_id`
  ↳ verify: a sample request shows up in App Insights with the tenant tag
- `[ ]` **P0-081** OpenTelemetry exporters (traces / metrics / logs)
  ↳ verify: traces visible across web → api in App Insights
- `[ ]` **P0-082** Sentry backend integration
  ↳ verify: unhandled exception appears in Sentry
- `[ ]` **P0-083** Better Stack uptime monitor on `/health`
  ↳ verify: monitor shows green; turn off API → alert fires
- `[ ]` **P0-084** Grafana dashboard placeholder (per-tenant request count)
  ↳ verify: dashboard URL resolves with one panel

### Documentation

- `[x]` **P0-D01** [CLAUDE.md](./CLAUDE.md) for AI agent context
- `[ ]` **P0-D02** `docs/runbooks/incident-response.md`
- `[ ]` **P0-D03** `docs/onboarding-new-tenant.md`
- `[x]` **P0-D04** Editorial design system + reveal primitives ([apps/web/components/editorial/](apps/web/components/editorial/)) — bonus polish, not on the original plan

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

## Phase 2 — SaaS commercialization  ▱▱▱▱▱▱▱▱▱▱  0%

Public-facing surface (landing page, pricing, terms / privacy), tenant onboarding wizard, white-label settings, admin consoles, go-live quality bar. ~25 tasks.

## Phase 3 — Real-time chat  ▱▱▱▱▱▱▱▱▱▱  0%

In-app chat via SignalR. Per-tenant rooms, message history, basic moderation. ~20 tasks.

## Phase 4 — Events + guests  ▱▱▱▱▱▱▱▱▱▱  0%

Picnic / fishing / tournament event types, family RSVP (kids/spouse), guest signup, photo gallery. ~25 tasks.

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

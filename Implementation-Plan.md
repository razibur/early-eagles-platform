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

**Estimated effort:** ~70 tasks · several months at any sustainable pace.

### Pre-work — accounts, domain, secrets

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-001 | Register `earlyeagles.club` via Cloudflare Registrar | S | — | Domain owned; auto-renew on |
| P0-002 | Create Cloudflare account + add domain DNS zone | S | P0-001 | Cloudflare manages the zone |
| P0-003 | Create Azure subscription; set up billing alerts at $50, $200, $500 | S | — | Alerts wired to email |
| P0-004 | Create GitHub organization (e.g., `early-eagles`) + private repo `early-eagles-platform` | S | — | Repo exists |
| P0-005 | Set up password manager vault (1Password / Bitwarden) for project secrets | S | — | Vault exists, owner has access |
| P0-006 | Create Clerk account + production application | S | — | Clerk app exists; API keys stored in vault |
| P0-007 | Create Stripe account + retrieve test/live API keys | S | — | Stripe account verified; keys in vault |
| P0-008 | Create Sentry project (backend + frontend) | S | — | Two Sentry DSNs in vault |
| P0-009 | Create Better Stack account (uptime + status page) | S | — | Account ready |
| P0-010 | Create PostHog project (cloud or self-hosted) | S | — | API key in vault |
| P0-011 | Create Resend account + verify `earlyeagles.club` for sending email | S | P0-002 | DKIM/SPF passing |
| P0-012 | Create Flagsmith project (cloud or self-hosted) | S | — | API key in vault |
| P0-013 | Create Cloudflare R2 bucket `ee-storage-dev` | S | — | Bucket exists; access keys in vault |

### Repository scaffolding

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-014 | Repo skeleton — README, LICENSE, `.gitignore`, `.editorconfig`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md` | M | P0-004 | Initial commit on `main` |
| P0-015 | Branch protection on `main`: require PR review, status checks, signed commits | S | P0-014 | Direct pushes blocked |
| P0-016 | `CODEOWNERS` file | S | P0-014 | Owner auto-requested on PRs |
| P0-017 | ADR template + `docs/adr/0000-template.md` | S | P0-014 | Template documented |
| P0-018 | Move existing docs (`Plan.md`, `project-scope.md`, `docs/`) into the new repo | S | P0-014 | Docs in repo |
| P0-019 | Set up monorepo layout: `apps/web`, `apps/api`, `infra`, `docs` (decide pnpm or npm workspaces; .NET solution alongside) | M | P0-014 | Folder structure committed |

### Backend — Clean Architecture solution

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-020 | `EarlyEagles.sln` with projects: `Api`, `Application`, `Domain`, `Infrastructure`, `Tests.Unit`, `Tests.Integration`, `Tests.Acceptance` | M | P0-019 | Solution builds; refs follow Clean Arch (no Domain → Infra) |
| P0-021 | Add base Domain types: `Entity<TId>`, `ValueObject`, `AggregateRoot`, `IDomainEvent` | S | P0-020 | Compiles; basic tests pass |
| P0-022 | StyleCop + Roslyn analyzers + `.editorconfig` rules | S | P0-020 | `dotnet build` enforces style |
| P0-023 | MediatR + FluentValidation + AutoMapper wiring | S | P0-020 | Sample command/query handler works |
| P0-024 | Pipeline behaviors: `LoggingBehavior`, `ValidationBehavior`, `TransactionBehavior`, `AuditBehavior` | M | P0-023 | Each behavior unit-tested |
| P0-025 | Centralized exception handling middleware → ProblemDetails | S | P0-020 | Errors return RFC 7807 JSON |
| P0-026 | Health check endpoints `/health` (live) and `/ready` (DB + Service Bus) | S | P0-020 | Endpoints respond correctly |
| P0-027 | OpenAPI / Swagger setup with auth + tenant header documented | M | P0-020 | `/swagger` shows endpoints |

### Multi-tenancy core

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-028 | `Tenant` aggregate (Id, Slug, DisplayName, Locale, Branding, Plan, Status) | S | P0-021 | Domain model unit-tested |
| P0-029 | `TenantMember` join entity (TenantId, ClerkUserId, Role) | S | P0-028 | Domain model unit-tested |
| P0-030 | `ITenantContext` interface + scoped service | S | P0-028 | Resolves current tenant per request |
| P0-031 | Tenant-resolution middleware: subdomain → `TenantContext` | M | P0-030 | `<slug>.earlyeagles.club` resolves correctly |
| P0-032 | EF Core `DbContext` with global query filter `e => e.TenantId == _tenant.Id` on every tenant-scoped entity | M | P0-030 | Filter automatic on all queries |
| P0-033 | First EF Core migration: `tenants`, `tenant_members`, `audit_events` | M | P0-032 | Migration applies cleanly |
| P0-034 | Tenant isolation **integration test**: Tenant A user cannot read Tenant B data via any endpoint | L | P0-032, P0-033 | Test exists in CI; fails loudly if filter bypassed |

### Identity — Clerk integration

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-035 | Clerk JWT validation middleware in `Api` | M | P0-006, P0-020 | Authenticated requests carry user + org |
| P0-036 | Clerk webhook receiver: `organization.created`, `organizationMembership.created`, `user.created`, `user.updated` | M | P0-035 | Webhooks update local `tenants` / `tenant_members` |
| P0-037 | Webhook signature verification (Svix) | S | P0-036 | Invalid signatures rejected |
| P0-038 | Map Clerk org roles ↔ tenant-admin / member | S | P0-036 | Authorization works in API |
| P0-039 | Frontend Clerk SDK setup in Next.js (`@clerk/nextjs`) | M | P0-006 | Sign-in / sign-up pages work locally |
| P0-040 | Sign-in methods enabled: Google + email magic link + phone OTP + MFA optional | S | P0-039 | All three work in dev |

### Billing — Stripe integration (scaffolding only — no UI yet)

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-041 | Stripe SDK setup (.NET + JS) | S | P0-007 | SDK initialized with secrets from Key Vault |
| P0-042 | Stripe products + prices in dashboard: Free / Pro / Enterprise (test mode) | S | P0-007 | Products exist with price IDs |
| P0-043 | Stripe webhook receiver: `checkout.session.completed`, `customer.subscription.*`, `invoice.payment_failed` | M | P0-041 | Webhooks dispatch domain events |
| P0-044 | Stripe webhook signature verification | S | P0-043 | Invalid signatures rejected |
| P0-045 | Map Stripe customer ↔ Tenant; Stripe subscription ↔ tenant Plan | M | P0-043 | Subscription state synced to `tenants` |

### Audit logging

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-046 | `AuditEvent` entity (TenantId, ActorId, Action, ResourceType, ResourceId, Metadata, Timestamp) | S | P0-021 | Append-only table |
| P0-047 | `IAuditService` + AuditBehavior pipeline that records every command | M | P0-024, P0-046 | Every state-changing op produces an audit row |
| P0-048 | Audit query endpoint (tenant-admin scope) | S | P0-046 | Tenant-admin can list own audit events |

### Domain events + Service Bus

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-049 | `IDomainEventDispatcher` + outbox pattern in `DbContext.SaveChangesAsync` | M | P0-032 | Events captured atomically with state changes |
| P0-050 | Outbox processor background service publishing to Service Bus | M | P0-049 | Events published reliably |
| P0-051 | Service Bus topic + subscriptions provisioned in Bicep (P0-058) | S | P0-058 | Topic exists in dev |
| P0-052 | First handler: `PaymentConfirmed` → log only (real handler in Phase 1) | S | P0-050 | Handler invoked when event published |

### Email + notifications scaffolding

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-053 | `IEmailSender` provider abstraction → Resend implementation | S | P0-011 | Sample email sends |
| P0-054 | Email template rendering (Razor or Liquid) with i18n | M | P0-053 | Renders en/bn |
| P0-055 | Smoke-test email from dev → owner inbox | S | P0-053 | Email delivered |

### Infrastructure as Code (Bicep)

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-056 | Naming convention + tagging strategy doc (`docs/infra-conventions.md`) | S | — | Naming rules written |
| P0-057 | Bicep modules: `resourceGroup`, `keyVault`, `logAnalytics`, `appInsights` | M | P0-003 | Modules parameterized by env |
| P0-058 | Bicep modules: `postgres-flexible`, `containerAppsEnv`, `containerApp`, `serviceBus`, `redis` | L | P0-057 | All modules deploy successfully |
| P0-059 | `infra/main.dev.bicep` orchestrating dev environment | M | P0-058 | `az deployment` provisions full dev env |
| P0-060 | `infra/main.staging.bicep` | S | P0-059 | Staging env deployed |
| P0-061 | `infra/main.prod.bicep` | S | P0-059 | Prod env deployed (empty, no traffic) |
| P0-062 | Managed identity for Container App → Postgres + Key Vault + Service Bus | M | P0-058 | Pod-less DB auth works |
| P0-063 | Document Bicep deploy runbook | S | P0-061 | `docs/runbooks/deploy-infra.md` exists |

### Frontend foundations

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-064 | `apps/web` Next.js 16 + TypeScript + ESLint + Prettier | S | P0-019 | `pnpm dev` runs |
| P0-065 | Tailwind + shadcn/ui setup; install base components | M | P0-064 | Sample page styled |
| P0-066 | `next-intl` with locale routing; en + bn message files; placeholder strings | M | P0-064 | `/en/` and `/bn/` routes work |
| P0-067 | `next-pwa` + manifest + icons (placeholder) | S | P0-064 | App installable to home screen |
| P0-068 | Web Push subscription scaffolding (VAPID keys generated; subscribe endpoint stubbed) | M | P0-064 | Subscription stored in DB |
| P0-069 | Bangla font loaded (Noto Sans Bengali) | S | P0-066 | Bangla renders cleanly |
| P0-070 | Layout shell + nav + theme switcher + locale switcher | M | P0-065, P0-066 | Layout responsive on mobile |
| P0-071 | TanStack Query + auth-aware fetch client | M | P0-039 | Authenticated calls succeed |
| P0-072 | Zustand store for client-only state | S | P0-064 | Sample slice works |
| P0-073 | Sentry frontend integration | S | P0-008 | Errors captured |
| P0-074 | PostHog SDK | S | P0-010 | Page views captured |
| P0-075 | Flagsmith SDK | S | P0-012 | Sample flag works |
| P0-076 | Cookie consent banner + cookie policy stub | M | P0-064 | Compliant default |
| P0-077 | Tenant subdomain routing in Next.js (rewrites) + dev hosts file note | M | P0-031, P0-064 | `<slug>.localhost:3000` resolves |

### CI/CD

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-078 | GitHub Actions: build + unit-test + lint on PR | M | P0-020 | Required check on `main` |
| P0-079 | GitHub Actions: integration tests against ephemeral Postgres | M | P0-033, P0-078 | Integration suite runs |
| P0-080 | Dependabot config for NuGet + npm | S | P0-014 | Dependabot opens PRs |
| P0-081 | GitHub secret scanning + push protection enabled | S | P0-014 | Secrets blocked from commits |
| P0-082 | CodeQL workflow | S | P0-078 | Security findings reported |
| P0-083 | SBOM generation (CycloneDX) on release | S | P0-078 | SBOM artifact published |
| P0-084 | GitHub Actions: deploy backend to dev on `main` merge | M | P0-059, P0-078 | Auto-deploy to dev works |
| P0-085 | GitHub Actions: deploy frontend to Vercel dev preview on PR | S | P0-064 | Preview URL on each PR |
| P0-086 | GitHub Actions: deploy to staging on tag (`v*-staging`) | S | P0-060, P0-084 | Tag triggers staging deploy |
| P0-087 | GitHub Actions: deploy to prod on **manual approval** in environment | M | P0-061, P0-086 | Manual gate enforced |
| P0-088 | Migrations runner job (separate from app deploy) | M | P0-033, P0-084 | Migrations apply before app rolls |

### Observability + ops

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-089 | OpenTelemetry traces in API; enrich with `tenant_id`, `user_id` | M | P0-030 | Traces in App Insights filterable by tenant |
| P0-090 | Structured logging (Serilog) with `tenant_id` enricher | S | P0-030 | Logs queryable per tenant |
| P0-091 | App Insights dashboard for key metrics (RPS, p95 latency, errors, DB calls) | M | P0-089 | Dashboard pinned |
| P0-092 | Better Stack uptime checks → `/health` on dev/staging/prod | S | P0-026 | Alerts wired to email |
| P0-093 | Better Stack public status page on `status.earlyeagles.club` | S | P0-009 | Status page live |
| P0-094 | Define error budget + alerting thresholds in a runbook | S | P0-091 | Document in `docs/runbooks/` |

### Documentation

| ID | Task | Effort | Depends on | Acceptance |
|---|---|---|---|---|
| P0-095 | README: project overview + quick start (local dev) | M | P0-064, P0-020 | New dev can `pnpm dev` + `dotnet run` from instructions |
| P0-096 | Architecture diagram (mermaid) in repo | S | — | Diagram renders in README |
| P0-097 | Runbooks: `deploy.md`, `rollback.md`, `db-migrate.md`, `incident-response.md` | M | P0-087 | Runbooks linked from README |
| P0-098 | Contribution guide: branching, commit format, PR template | S | P0-014 | `CONTRIBUTING.md` complete |
| P0-099 | Glossary of domain terms (`docs/glossary.md`) | S | — | Tenant, Member, Session, RSVP, etc. defined |

### Phase 0 acceptance criteria

Phase 0 is **done** when:

- [ ] Dev, staging, and prod environments are all provisioned and healthy via Bicep.
- [ ] A new pull request runs build + unit + integration + isolation tests, and on merge auto-deploys to dev.
- [ ] Tenant isolation integration test passes (Tenant A cannot see Tenant B data).
- [ ] An authenticated user can sign in via Clerk in all three environments.
- [ ] A test Stripe webhook updates a tenant's plan locally.
- [ ] A test email sends end-to-end via Resend in en and bn.
- [ ] App Insights, Sentry, PostHog, Better Stack, and Flagsmith are all collecting events from at least one environment.
- [ ] Status page resolves at `status.earlyeagles.club`.
- [ ] All runbooks exist and the owner has done a successful manual deploy of each environment.

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

**Estimated effort:** ~15 tasks.

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
| P3-014 | Bangla translations | S | Strings translated |
| P3-015 | E2E + load tests for chat | L | Tests green |

---

## Phase 4 — Events + guests

**Goal:** Picnic, fishing, and tournament events; non-member guests can RSVP via email signup.

**Estimated effort:** ~20 tasks.

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
| P4-019 | Bangla translations | Strings translated |
| P4-020 | E2E tests | Tests green |

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

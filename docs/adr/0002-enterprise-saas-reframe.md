# ADR-0002: Reframe to enterprise B2B SaaS — multi-tenant from day one

- **Status:** Accepted (operative — supersedes most of ADR-0001)
- **Date:** 2026-05-08
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Plan.md`](../../Plan.md), [`project-scope.md`](../../project-scope.md), [ADR-0001](./0001-stack-and-architecture.md), [pre-BrainStorm Supabase alternative](../alternatives/pre-brainstorm-supabase-stack.md)

## Context

ADR-0001 chose ASP.NET Core + Firebase Auth + SignalR + Cloudflare R2 + PostgreSQL for a "future-proof but ultimately for one club" platform, with multi-tenancy filed as an *optional* Phase 6 if other clubs ever asked.

The owner has now stated explicit intent to:

1. Build at **enterprise level** — production-grade engineering, not hobby quality.
2. Make the platform **customizable for other badminton clubs**.
3. **Sell** it commercially in the future.

This is not a small adjustment to ADR-0001. The destination changed from "multi-surface app for one club" to "multi-tenant B2B SaaS where Early Eagles is Tenant 0." Several decisions in ADR-0001 were correct for the old framing and wrong for the new one.

## Decision

Adopt the following enterprise-SaaS architecture and stack, superseding the matching parts of ADR-0001.

### Architecture changes

1. **Multi-tenancy is Phase 0, not Phase 6.** Pool model: single Postgres database, `tenant_id` on every relevant table, EF Core global query filters, tenant-resolution middleware (subdomain + JWT claim), automated isolation tests in CI. DB-per-tenant available as Enterprise-tier upsell later.
2. **Identity = Clerk B2B Organizations** (replaces Firebase Auth). Each tenant is a Clerk Organization. Members are Clerk users with per-org roles. B2B SSO (SAML/OIDC) available on Clerk's Pro/Enterprise plans for Phase 7.
3. **Subscription billing = Stripe Billing.** Tenants subscribe to Free / Pro / Enterprise tiers. Stripe handles invoicing, dunning, tax, metering.
4. **Three environments + IaC.** Dev → Staging → Prod, all provisioned via Bicep. Manual promote to prod. No clicked-in-the-portal infrastructure.
5. **Observability as a first-class product.** Application Insights + OpenTelemetry traces tagged with `tenant_id`; Sentry for errors; Better Stack for uptime; Grafana dashboards; per-tenant log isolation.
6. **Audit logging from day one.** Append-only `audit_events` table per tenant; tenant-admins search their own audit; platform-admins search across tenants for support and incident response.
7. **Feature flags per tenant** via Flagsmith. Every new feature ships behind a flag.
8. **Security baseline.** TLS 1.3, Key Vault for secrets, dependency + secret scanning in CI, SBOM generated, WAF in front, encryption at rest. SOC 2 readiness as a posture even before the audit.
9. **Cloud platform = Azure** (with cloud-portable abstractions where possible). .NET-first, Azure-native services across the board (Container Apps, Postgres Flexible Server, Service Bus, Cache for Redis, SignalR Service, Application Insights).

### Stack changes from ADR-0001

| Layer | ADR-0001 | ADR-0002 (now) | Reason |
|---|---|---|---|
| Auth | Firebase Auth | **Clerk B2B Organizations** | Native multi-tenant primitive; B2B SSO upgrade path; better DX for Next.js. Unity (Phase 9) can use OIDC against Clerk. |
| DB host | Neon Postgres | **Azure Database for PostgreSQL — Flexible Server** | HA, PITR, read replicas, SLA, fits Azure-native operations |
| Realtime | SignalR (self-hosted) | **SignalR + Azure SignalR Service** | Managed scale-out for many tenants |
| Cache + locks | (none) | **Azure Cache for Redis** | Required at scale; sessions, hot-data caching, distributed locks |
| Message bus | (none — in-process events) | **Azure Service Bus** | Reliable cross-service events |
| Backend hosting | Render free / Fly.io free / Azure App Service B1 | **Azure Container Apps (paid)** | Free tiers don't meet SaaS SLAs |
| Frontend hosting | Vercel Hobby | **Vercel Pro** | Hobby is non-commercial license |
| CDN + WAF | (none) | **Cloudflare Pro in prod** | DDoS, WAF, edge caching |
| IaC | (informal) | **Bicep** | Reproducible environments mandatory |
| Observability | Sentry only | **Application Insights + OpenTelemetry + Sentry + Better Stack + Grafana** | Real production telemetry |
| Feature flags | env-based | **Flagsmith** | Per-tenant rollouts |
| Subscription billing | (not in scope) | **Stripe Billing** | Required to commercialize |
| Product analytics | (none) | **PostHog** | Understand tenant usage |
| Customer support tooling | (none) | **Crisp + Better Stack status page** | Required for paying customers |
| Legal | (none) | **ToS / Privacy / DPA / Cookie Policy** (Termly or hand-drafted) | Required to sell |

Stack components from ADR-0001 that **stay**:

- ASP.NET Core 9 + Clean Architecture + EF Core 9 (backend)
- Next.js 16 + TypeScript + Tailwind + shadcn/ui (frontend)
- PostgreSQL (database engine; only the host changes)
- Cloudflare R2 (object storage)
- Resend (transactional email)
- GitHub Actions (CI/CD)
- Azure Key Vault (secrets)
- Domain `earlyeagles.club` via Cloudflare Registrar

### Phasing changes

ADR-0001 had 8 phases; ADR-0002 has **9 phases** with reordering:

1. Phase 0 — **Enterprise Foundations** (substantially expanded: multi-tenancy, IaC, observability, audit, Clerk + Stripe scaffolding all enter here)
2. Phase 1 — Attendance core (Tenant 0 only)
3. Phase 2 — **SaaS commercialization** (NEW — was Phase 6 in ADR-0001)
4. Phase 3 — Real-time chat
5. Phase 4 — Events + guests
6. Phase 5 — AI Coach
7. Phase 6 — Mobile app
8. Phase 7 — **Enterprise tier (SSO, SLA, dedicated infra)** (NEW)
9. Phase 8 — M365 Teams Agent (optional)
10. Phase 9 — XR / MatchCourt (long-horizon)

## Consequences

### Positive

- **Sellable platform.** Multi-tenancy, billing, white-label, audit, and SSO upgrade path are baked in — not retrofitted. A second tenant can onboard in minutes after Phase 2 ships, not "a quarter of refactor work."
- **Tenant isolation correctness from day one.** EF Core query filters + middleware + automated isolation tests in CI prevent the worst class of B2B SaaS bugs (data leak across tenants).
- **Operational maturity.** IaC, three environments, real observability, audit logs, on-call-ready monitoring. Production incidents become diagnosable rather than mysterious.
- **Identity that fits the product.** Clerk Organizations match "club = tenant" 1:1. Member-to-org relationships, per-org roles, invitation flows, and B2B SSO upgrade path are built-in primitives, not code we maintain.
- **Self-funding path.** At $25–40/month/club Pro tier, 5–10 paying customers covers infrastructure. Beyond that, it's a real business.
- **Compliance posture.** Encryption at rest + audit logs + secret management + dependency scanning + SBOM = SOC 2 readiness without heroic effort when the first enterprise customer asks.

### Negative

- **Phase 0 is now substantially larger.** Multi-tenancy patterns, IaC, observability, audit, Clerk + Stripe scaffolding, three environments — significantly more upfront work than ADR-0001's Phase 0.
- **Cost jumps from $12/year to $50–80/month.** This is the price of being sellable. Vercel Pro, paid Postgres, paid auth tier (eventually), monitoring, etc. Recoverable as soon as Phase 2 ships and the first paying tenant signs up.
- **More services to operate.** ~12 services and dashboards (Azure, Vercel, Clerk, Stripe, Cloudflare, Sentry, Better Stack, PostHog, Flagsmith, Resend, Crisp, GitHub) vs the simpler 4–5 in ADR-0001.
- **Higher cognitive load for solo developer.** Cloud architect skills + B2B SaaS patterns + .NET + Next.js + IaC. Steeper learning curve.
- **Vendor lock-in to Azure** (partially). Most services have portable equivalents on AWS / GCP, but Bicep is Azure-specific and would need rewriting in Terraform if the platform ever moves clouds. Acceptable trade-off given .NET-on-Azure economics.

### Neutral

- **Firebase Auth dropped despite original Unity-SDK justification.** Phase 9 XR is too speculative to anchor a B2B auth choice. Unity can authenticate to Clerk via standard OIDC.
- **Multi-tenancy pool model chosen over silo or bridge.** Best fit for small tenants (clubs with 25–500 members). Silo (DB-per-tenant) becomes available as Enterprise-tier upsell in Phase 7.
- **Cloudflare stays for DNS and (later) CDN/WAF**, even though Azure has equivalents (Front Door). Cloudflare's free tier is generous and the existing domain is registered there.

## Alternatives considered

### Alt A — Stay with ADR-0001 stack and bolt SaaS features on later

**Why considered:** Less change. Build for one club, retrofit multi-tenancy when needed.

**Why rejected:** Retrofitting multi-tenancy onto a single-tenant codebase is one of the most expensive refactors in B2B SaaS. EF Core query filters, middleware, every test, every migration — all need to change. Doing it day one is comparatively cheap. Doing it after Phase 4 features ship would invalidate weeks of work.

### Alt B — Auth0 instead of Clerk

**Why considered:** Auth0 is more enterprise-credentialed (Fortune 500 buyers know the name); has Unity SDK; mature SSO.

**Why rejected:** Auth0's B2B story (organizations) is newer and less polished than Clerk's. Auth0's pricing is meaningfully higher at scale. Most badminton-club customers won't recognize either brand — credentialing is a smaller factor than DX and the organizations primitive.

### Alt C — WorkOS for B2B SSO + Firebase Auth for consumer

**Why considered:** WorkOS is purpose-built for B2B SSO with the cleanest pricing model (per-org).

**Why rejected:** Two auth systems = double the integration work. Clerk delivers both consumer auth and B2B SSO from one platform. Reconsider WorkOS only if Clerk's SSO offering proves inadequate at Phase 7.

### Alt D — AWS instead of Azure

**Why considered:** AWS is the largest cloud, most B2B SaaS templates target it, broadest service catalog.

**Why rejected:** .NET on Azure has first-party tooling, lower friction, and (often) lower cost. Azure Container Apps + Azure Postgres Flexible Server + Service Bus are well-integrated for the .NET stack. Switching to AWS means ECS/Fargate + RDS + SQS — workable but more work for marginal benefit. Cloud-portable abstractions in Bicep / .NET keep AWS as a future option if Azure pricing or politics ever turn.

### Alt E — Silo multi-tenancy (DB-per-tenant) from day one

**Why considered:** Strongest tenant isolation; simplest data residency story.

**Why rejected:** ~10–50× the operational and cost overhead at small scale. Pool model with rigorous query filtering is the industry standard for small/medium B2B SaaS. Silo becomes available later as an Enterprise-tier upsell for customers who require it.

## Revisit triggers

Re-open this ADR (write ADR-0003+) if:

- Cost of Azure Container Apps + Postgres Flexible Server proves prohibitive at low scale (consider AWS Fargate + RDS or stay on free tiers longer).
- Clerk's B2B SSO proves inadequate at first Enterprise customer (consider Auth0 / WorkOS).
- A customer requires data residency that Azure can't serve at acceptable cost (consider GCP / on-prem option).
- Phase 9 XR is dropped entirely (auth choice can be re-simplified).
- A second cloud is required for redundancy (multi-cloud rewrite of Bicep → Terraform).

ADRs are immutable. Future changes are recorded as ADR-0003, ADR-0004, etc.

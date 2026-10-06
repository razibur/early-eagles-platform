# Early Eagles — Master Plan (Enterprise B2B SaaS)

> **This document is the single source of truth for the Early Eagles platform plan.** It supersedes the seven documents in `BrainStorm/`. Decision rationale lives in `docs/adr/`. ADR-0001 captured the original direction; **ADR-0002 captures the reframe to enterprise B2B SaaS** and is now the operative architecture decision.

Companion documents:

- [`project-scope.md`](./project-scope.md) — Problem · Solution · Features
- [`docs/adr/0001-stack-and-architecture.md`](./docs/adr/0001-stack-and-architecture.md) — original stack rationale (mostly superseded)
- [`docs/adr/0002-enterprise-saas-reframe.md`](./docs/adr/0002-enterprise-saas-reframe.md) — **operative architecture decision**
- [`docs/alternatives/pre-brainstorm-supabase-stack.md`](./docs/alternatives/pre-brainstorm-supabase-stack.md) — earliest rejected option

---

## 1. Mission and constraints

| | |
|---|---|
| **Vision** | Build a production-grade, multi-tenant SaaS platform for badminton clubs. Early Eagles is **Tenant 0** — we eat our own dogfood. Other clubs become paying customers. |
| **Mode** | Enterprise-level engineering. Quality, security, scalability, sellability over speed. |
| **Time** | No fixed deadline. Foundations done right, then features. |
| **Tenant 0** | Early Eagles — ~35 members, weekly Sunday play, $15 Zelle, occasional picnic / fishing |
| **Future tenants** | Other badminton clubs (initially small Bangladeshi-diaspora clubs in U.S./Canada; later open) |
| **Budget envelope** | $50–80/month at Phase 1, scales with revenue thereafter |

## 2. Locked product decisions (Tenant 0 = Early Eagles)

| Topic | Decision |
|---|---|
| Tenancy | Multi-tenant from day one. Pool model (single DB, `tenant_id` on every relevant table) for Free/Pro tiers. DB-per-tenant available as Enterprise-tier upsell later. |
| Admins | Per-tenant role-based (tenant-admin, member, guest). Platform-admin role separate, scoped to operators. |
| Payment (member dues) | Zelle only at Tenant 0. Tenants configure their own preferred payment method (Zelle / e-Transfer / cash) — track only, do not collect. |
| Subscription billing | Stripe Billing. Tiers: Free (1 club, ≤25 members, basic) → Pro (≤200 members, all features) → Enterprise (SSO, audit, SLA, custom). |
| Leaderboard | Per-tenant; public to that tenant's members by default; tenant-admin can flip to private. |
| Auth methods | Google + email magic link + phone OTP — all three. B2B SSO (SAML/OIDC) on Enterprise tier. |
| Languages | English + Bangla day one; i18n architecture supports more. |
| Guests | Email signup; per-tenant "guest" role; not in leaderboard; cannot be promoted. |
| App format | PWA in v1; native (Flutter or Expo) in Phase 6. |
| Real-time chat | Phase 3 (after attendance + SaaS commercialization). |
| Date format | "Wednesday, January 1, 2025"; week navigator with arrows; future-day arrow greyed at today. |

## 3. Architecture principles (enterprise edition)

```
       Tenant 0 (Early Eagles)   Tenant 1   Tenant 2   ...   Tenant N
              \                    |          |              /
               \                   |          |             /
                +------------------+----------+------------+
                                   |
                  Web PWA · Mobile · M365 Agent · Unity XR
                                   |
                       REST + SignalR + OpenAPI
                                   |
              Cloudflare (CDN, WAF, DDoS, TLS)
                                   |
              Azure Container Apps — ASP.NET Core 10 (Clean Arch)
                                   |
                    Tenant-resolution middleware
                                   |
        Domain | Application | Infrastructure | API
                                   |
   +--------+--------+--------+----+----+--------+--------+--------+
   |        |        |        |        |        |        |        |
   v        v        v        v        v        v        v        v
 Postgres  Redis  Service   R2/Blob  Clerk   Stripe   Application  Sentry
 (Flexible (cache  Bus      (files)  (auth +  Billing  Insights    (errors)
  Server)  + locks) (events)         orgs)              + Otel
```

### Twelve principles

1. **API-first.** Every feature is a documented OpenAPI endpoint before any UI consumes it.
2. **Clean Architecture.** Domain has zero dependencies; Infrastructure swappable; testable in isolation.
3. **Multi-tenant by default.** `tenant_id` on every domain table; EF Core global query filters; tenant-resolution middleware; automated isolation tests in CI.
4. **Federated identity via Clerk Organizations.** Each club is a Clerk Organization. Members belong to one or more organizations with per-org roles.
5. **Event-driven.** Domain events (`PaymentConfirmed`, `RsvpUpdated`, `MemberJoined`) published to Azure Service Bus; consumers handle email, push, audit logging, AI summaries.
6. **Containerized + IaC.** Backend in Docker; infrastructure in Bicep. Every environment reproducible; new region or new tenant cluster in hours.
7. **Three environments minimum.** Dev → Staging → Prod, with infrastructure parity. Manual promote to prod.
8. **Observability first-class.** OpenTelemetry traces tagged with `tenant_id`; Application Insights for production telemetry; Sentry for errors; Better Stack for uptime; structured logs.
9. **Audit log from day one.** Append-only `audit_events` table per tenant; tenant-admins can search their own audit; platform-admins can search across tenants for support.
10. **Feature flags per tenant.** Flagsmith — every new feature ships behind a flag, rolled out tenant-by-tenant.
11. **Security baseline.** TLS 1.3, Key Vault for secrets, dependency + secret scanning in CI, SBOM generated, WAF in front, encryption at rest (managed by Azure Postgres).
12. **i18n at every layer.** Backend errors, emails, push, UI labels — en/bn from day one.

## 4. Technology stack

| Layer | Pick |
|---|---|
| Backend | ASP.NET Core 10 Web API + Clean Architecture + EF Core 10 + tenant middleware (per [ADR-0006](./docs/adr/0006-target-dotnet-10-lts.md)) |
| Database | Azure Database for PostgreSQL — Flexible Server (HA, point-in-time recovery, read replicas) |
| Cache + locks | Azure Managed Redis ([ADR-0008](./docs/adr/0008-azure-managed-redis.md)) |
| Message bus | Azure Service Bus (domain events) |
| Realtime | SignalR + Azure SignalR Service (managed scale-out) |
| Auth | Clerk B2B (Organizations) — Google + email magic link + phone OTP, MFA, B2B SSO upgrade |
| Object storage | Cloudflare R2 (S3-compatible). Azure Blob as fallback for data-residency contracts. |
| Frontend | Next.js 16 (App Router) + TypeScript + Tailwind + shadcn/ui |
| PWA | `next-pwa` + Web Push (VAPID) |
| i18n | `next-intl` + `IStringLocalizer` (en + bn) |
| Data fetching | TanStack Query + Zustand |
| Email | Resend (transactional) + SendGrid or Azure Communication Services (bulk) |
| Push (mobile) | FCM (Phase 6) |
| Subscription billing | Stripe Billing (tiers, metering, invoicing, dunning, tax) |
| Feature flags | Flagsmith (self-host or cloud) |
| Product analytics | PostHog |
| CDN + WAF + DDoS + TLS | Cloudflare (Pro plan in prod) |
| Frontend hosting | Vercel Pro |
| Backend hosting | Azure Container Apps (autoscale, blue/green, revisions) |
| IaC | Bicep |
| CI/CD | GitHub Actions (per-environment workflows; manual prod promote) |
| Observability | Application Insights + OpenTelemetry + Sentry + Better Stack uptime + Grafana dashboards |
| Secrets | Azure Key Vault (per environment) |
| Customer support | Crisp (in-app widget) |
| Status page | Better Stack |
| Mobile (Phase 6) | Flutter or Expo (decided then) |
| AI Coach (Phase 5) | OpenAI / Anthropic via internal provider abstraction |
| XR (Phase 9) | Unity + Meta XR SDK consuming the same API via OIDC |
| Domain | `earlyeagles.club` (Cloudflare Registrar) — primary brand. Tenants get subdomains (`<tenant>.earlyeagles.club`) and optional custom domains. |
| Legal | Termly (or hand-drafted) — ToS, Privacy, DPA, Cookie Policy |

See [ADR-0002](./docs/adr/0002-enterprise-saas-reframe.md) for the rationale on each pick and the alternatives rejected.

## 5. Roadmap

| Phase | Scope | Outcome |
|---|---|---|
| **Phase 0 — Enterprise Foundations** | Clean Arch skeleton, multi-tenant data model + middleware + isolation tests, Bicep IaC, dev/staging/prod envs, observability, audit log infra, Clerk + Stripe integration scaffolding, GitHub Actions, secret scanning, ADR practice | Production-grade platform ready to run features |
| **Phase 1 — Attendance core (Tenant 0)** | RSVP, Zelle payment, leaderboard, week navigator, announcements + comments, PWA, en/bn — Tenant 0 only, no public signup yet | Early Eagles uses it weekly |
| **Phase 2 — SaaS commercialization** | Public signup → Stripe checkout → tenant provisioning, tenant-admin console, white-label (custom domain, logo, theme, copy), platform-admin console | Other clubs can sign up and pay |
| **Phase 3 — Real-time chat** | SignalR group + DMs, image attach (R2), web push, presence, typing | Replaces WhatsApp dependency for tenants who opt in |
| **Phase 4 — Events + guests** | Picnic / fishing / tournament event types, family RSVP (kids/spouse), guest signup, photo gallery | Full event-management surface |
| **Phase 5 — AI Coach** | Coach persona endpoint, en/bn humor, drills/tactics/gear, per-tenant configurable | Differentiation, retention |
| **Phase 6 — Mobile app** | Flutter or Expo client, FCM push, offline-first | Native app stores presence |
| **Phase 7 — Enterprise tier** | B2B SSO (SAML/OIDC) via Clerk, advanced audit log search, SLA contracts, dedicated-DB / region option, SCIM provisioning | Sellable to larger orgs / federations |
| **Phase 8 — M365 / Teams Agent (optional)** | Declarative Agent / Copilot consuming the same API | Surface for Teams-using customers |
| **Phase 9 — XR / MatchCourt (long-horizon)** | Unity client, Meta XR SDK, virtual clubhouse, remote rally | Long-term moonshot |

The decisions made in **Phase 0** (multi-tenancy, IaC, observability, Clerk Organizations, audit logs) are what make Phases 2–9 possible **without rewrites**.

## 6. Pricing model (sketch — refine before Phase 2)

| Tier | Price | Limits | Target |
|---|---|---|---|
| **Free** | $0 | 1 club, ≤25 members, attendance + leaderboard only | Hook |
| **Pro** | ~$25–40/month per club | Unlimited members, chat, events, AI coach, custom subdomain, en/bn | Most clubs |
| **Enterprise** | Custom (~$200+/month) | SSO, audit, SLA, dedicated DB / region, custom domain, support | Federations, larger orgs |

Stripe Billing supports all three with metering on member count if needed.

## 7. Cost model

| Stage | Monthly | Notes |
|---|---|---|
| **Phase 0 + 1** (Tenant 0 only) | $50–80 | Azure Container Apps minimum, Postgres Flexible Server B1ms (~$25), Vercel Pro $20, Cloudflare free, Clerk free, Stripe %, Resend free |
| **Phase 2** (5–10 paying clubs) | $150–250 | Postgres scales up, Clerk Pro $25, paid Cloudflare Pro $25, monitoring tier |
| **Phase 5–6** (50+ tenants) | $400–800 | Larger DB, Azure SignalR Service, Redis, FCM, AI API spend |
| **Phase 7** (Enterprise tier active) | $800–2K | Dedicated infra per Enterprise tenant, support tooling, expanded observability |

**Self-funding threshold:** at $25–40/month/club Pro tier, **5–10 paying clubs makes the platform pay for itself.** Beyond that, it becomes a real business.

## 8. Ideas explicitly parked

- **Microsoft 365 Teams Agent** (`Assistant-Idea.md`, `Agent-Idea.md`) → Phase 8. Optional secondary surface; consumed via the same API.
- **AI Coach** (`Coach-Idea.md`) → Phase 5. Bolts onto existing API; per-tenant configurable.
- **Flutter mobile** (`Mobile-App-Idea.md`) → Phase 6. Specific framework decided then.
- **XR / MatchCourt** (`XR-MatchCourt-Idea.md`) → Phase 9.
- **Marketplace / fundraising** (App-Idea Phase 3) → revisit after Phase 7; only if customer demand exists.

## 9. Out of scope (deliberate non-goals)

- Online member-dues collection (Stripe / bKash / etc.) — track Zelle/preferred method, do not collect. Stripe is used **only** for SaaS subscription billing of tenants.
- SEO marketing site beyond a simple landing page — handled separately when Phase 2 ships.
- Court / equipment booking conflicts.
- Generic tournament-bracket engine — round-robin / single-elim only.
- Replacing WhatsApp casual chat in Phase 1 — Phase 3 introduces in-app chat.

## 10. Decisions log

All architectural decisions live under [`docs/adr/`](./docs/adr/). When revisiting any decision, write a new ADR rather than editing existing ones — ADRs are immutable history.

- ADR-0001 — Original ASP.NET / Firebase / SignalR decision (largely superseded by ADR-0002)
- **ADR-0002 — Enterprise B2B SaaS reframe** (operative)

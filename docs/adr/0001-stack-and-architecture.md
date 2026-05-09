# ADR-0001: Stack and architecture for the Early Eagles platform

- **Status:** Accepted
- **Date:** 2026-05-08
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Plan.md`](../../Plan.md), [`project-scope.md`](../../project-scope.md)

## Context

Early Eagles is a small badminton club (~35 members, middle-aged Bangladeshi men). The owner wants to build a platform that:

1. Solves immediate club coordination pain (RSVP, Zelle payment tracking, attendance leaderboard, weekly schedule view, bilingual en/bn).
2. Grows over years into a multi-surface system (web → mobile → AI coach → optional Teams agent → optional XR client) **without being rewritten** at each step.
3. Stays cheap (target $12–30/year at club scale).
4. Has **no time pressure** — the owner explicitly chose the "full future-proof" mode and is willing to invest in foundations before shipping features.

The seven documents in `BrainStorm/` proposed three competing directions (standalone web app on ASP.NET Core; Flutter mobile-first with .NET backend; Microsoft 365 Declarative Agent inside Teams). They cannot all be the primary surface, but they can all coexist as *clients of the same API*. That observation drives this ADR.

## Decision

Adopt the following architecture and stack:

### Architecture
- **API-first.** A single backend exposes REST + SignalR + OpenAPI. Web, future mobile, future Unity XR, and an optional future Teams agent all consume it. No surface owns its own data store.
- **Clean Architecture** in the backend (Domain / Application / Infrastructure / API layers).
- **Multi-tenant data model from day one** — every relevant table has `club_id`.
- **Containerized** backend (Docker) — no cloud lock-in.
- **Event-driven seam** — domain events for cross-cutting concerns (email, push, future AI summaries).
- **Feature flags from day one** so phases roll out independently.
- **i18n (en + bn) at every layer**, not just the UI.

### Stack

| Layer | Pick |
|---|---|
| Backend | ASP.NET Core 9 Web API + EF Core 9 + Clean Architecture |
| Database | PostgreSQL (Neon free → managed prod) |
| Realtime | SignalR |
| Auth | Firebase Auth |
| Object storage | Cloudflare R2 |
| Frontend | Next.js 16 + TypeScript + Tailwind + shadcn/ui |
| PWA | `next-pwa` + Web Push (VAPID) |
| i18n | `next-intl` + `IStringLocalizer` |
| Email | Resend |
| Mobile (later) | Flutter or Expo, chosen at Phase 5 |
| AI (later) | OpenAI / Anthropic via provider abstraction |
| Domain / DNS | `earlyeagles.club` via Cloudflare Registrar + Cloudflare DNS |
| Frontend hosting | Vercel Hobby or Cloudflare Pages |
| Backend hosting | Azure Container Apps / Fly.io / Render (containerized) |
| CI/CD | GitHub Actions |
| Observability | Sentry + OpenTelemetry |

## Consequences

### Positive
- **One backend, many clients.** Mobile, Teams agent, AI coach, and XR all reuse the same API and identity. No parallel codebases drifting.
- **Tenant-ready.** If other Bangladeshi badminton clubs ask to join, multi-tenancy is a config flip, not a refactor. Could become an actual SaaS.
- **Portable hosting.** Docker + Postgres + static frontend means we can move providers in a weekend if pricing or politics change.
- **Cheap.** Free tiers cover club scale entirely; only the domain costs real money.
- **AI-ready.** When the Coach (Phase 4) ships, it just reads the existing tables.
- **XR-ready.** Firebase Auth is the only auth provider on the shortlist that natively supports a Unity client — chosen specifically to keep Phase 8 viable.

### Negative
- **More moving parts than a Supabase-only stack.** Backend, frontend, Firebase, R2, Postgres, and email are five separate systems. Operational surface area is larger.
- **Cold starts on free .NET hosting** (Render free / Fly.io free). First request after idle takes ~5–10 seconds. Acceptable at club scale; can be solved with $7/month warm tier if it ever annoys members.
- **Higher upfront cost in Phase 0.** Clean Architecture skeleton, OpenAPI generation, Docker, CI/CD, ADR practice, i18n scaffolding — all before any business feature. This is intentional: the owner chose "no time pressure" precisely to make this investment.
- **More for the solo developer to learn.** Firebase Admin SDK in .NET, SignalR, multi-tenancy patterns, Clean Architecture if not already familiar.

### Neutral
- **Microsoft 365 Agent direction is parked, not killed.** Phase 7 reinstates it as a *secondary* surface that consumes the same API.
- **Mobile framework (Flutter vs Expo) is deliberately deferred** to Phase 5 to avoid premature commitment.

## Alternatives considered

### Alt A: Supabase + Next.js (the original quick stack)

**What it was:** Next.js 16 full-stack on Vercel + Supabase (Postgres + Auth + Realtime + Storage + Row-Level Security).

**Why it was attractive:** Fastest to ship — one service for auth, db, realtime, storage. Free tier covers club scale. Less operational surface area.

**Why it lost:**
- Supabase Auth does not natively support Unity (XR client in Phase 8).
- Tighter coupling between data and BaaS makes a future migration painful.
- Less clean separation between UI and API — Next.js Server Components encourage UI to query the DB directly, which conflicts with the "API-first, many clients" principle.
- Owner chose "full future-proof" mode, where the Supabase advantage (speed) does not apply.

This stack would be the right answer in a different mode (limited time, single web surface only).

### Alt B: ASP.NET Core + Clerk Auth + Pusher Realtime

**Why it was attractive:** Clerk has the cleanest auth UX of any provider; Pusher is a battle-tested realtime BaaS.

**Why it lost:**
- Clerk also does not have first-party Unity support.
- Pusher adds a paid third-party dependency that SignalR (built into .NET) replaces for free.
- Two extra services on the bill for marginal benefit.

### Alt C: Flutter + .NET (Mobile-first, per `Mobile-App-Idea.md`)

**Why it was attractive:** One codebase for iOS + Android. The owner's brainstorm doc argues for this strongly.

**Why it lost (for v1):**
- Web is the necessary first surface — admins do confirmations from a desktop, members are mixed mobile/desktop, and shipping a web PWA is an order of magnitude cheaper than App Store + Play Store distribution.
- Building Flutter v1 would *delay* getting the club onto the platform by months for no v1 user benefit.
- A native app remains in the plan as Phase 5; the API-first design ensures the Flutter (or Expo) client at that point reuses everything.

### Alt D: Microsoft 365 Declarative Agent (per `Assistant-Idea.md` / `Agent-Idea.md`)

**Why it was attractive:** Live inside Teams + Copilot, no separate UI to build, modern AI surface.

**Why it lost (for v1):**
- The club communicates over WhatsApp, not Teams. Forcing 35 middle-aged users to install Teams + acquire a Copilot license is a non-starter.
- A Teams agent is not a *replacement* for a club platform — it would be a *secondary surface*. That is exactly what Phase 7 makes it.

### Alt E: SQLite as the production database (per `App-Idea.md` and `sprint-start-Idea.md`)

**Why it was attractive:** Zero-config, no separate DB process.

**Why it lost:**
- Limits to single-host deployment, which conflicts with containerized portable hosting.
- No realtime replication, no managed backups at scale.
- Postgres has a free tier (Neon) at exactly the same dollar cost — no reason to take SQLite's limitations.
- SQLite stays as a possible local-dev convenience but is not the production target.

## Revisit triggers

This ADR should be revisited if any of the following happen:

- Club grows past ~500 active users (re-evaluate hosting tier).
- A second club asks to join (Phase 6 SaaS becomes real, not optional).
- Firebase Auth pricing or terms change unfavorably.
- Owner decides to drop the XR ambition (could simplify auth choice back to Clerk/Supabase).
- Mobile (Phase 5) timing accelerates — re-confirm Flutter vs Expo choice with current ecosystem state.

When revisiting, write **ADR-0002+** rather than editing this document. ADRs are immutable history.

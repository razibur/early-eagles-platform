# Early Eagles — Project Scope (Enterprise B2B SaaS)

## Problem

Small badminton clubs run on WhatsApp threads, spreadsheets, and the organizer's memory. The Early Eagles club (~35 members, mostly middle-aged Bangladeshi men in the U.S.) is a typical example:

- **No source of truth for who is playing this Sunday.** Threads scroll past; late-comers miss the count; the admin has to re-tally.
- **Payment tracking is manual and lossy.** The admin chases members to confirm Zelle transfers and remembers payment status in their head. Mistakes happen.
- **No historical record.** Who is the most active member? Who has not shown up in months? Today, nobody can answer.
- **Event organization is ad-hoc.** Family picnics and men-only fishing trips repeat the same WhatsApp chaos every time.
- **No bilingual support.** Some members are more comfortable in Bangla than English.
- **No platform for future ambitions.** Ideas like an AI badminton coach, a mobile app, or an XR badminton experience have nowhere to live.

This pattern is not unique to Early Eagles. Hundreds of similar small clubs (Bangladeshi-diaspora, South-Asian-diaspora, and broader recreational badminton clubs across U.S. and Canada) face the same friction.

## Solution

A production-grade, multi-tenant **B2B SaaS platform for badminton clubs**. Each club is a tenant with its own members, sessions, payment ledger, leaderboard, events, branding, and (optionally) chat, AI coach, and mobile app.

**Early Eagles is Tenant 0** — the platform's first customer, used weekly to validate every feature before it reaches paying tenants.

Three commitments shape every architectural and product decision:

1. **API-first.** A single backend powers many surfaces (web today; mobile, Microsoft 365 agent, Unity XR tomorrow). Every feature is a documented endpoint before any UI consumes it.
2. **Multi-tenant by default.** Tenant isolation is enforced at every layer (data, identity, audit). A new club onboards in minutes.
3. **Sellable from Phase 2.** Pricing tiers, white-label (custom domain, theme, logo), per-tenant configuration, and Stripe Billing integration are built in — not bolted on later.

## Business model

| Tier | Target customer | Price (sketch) |
|---|---|---|
| **Free** | A single small club hooked on the basics | $0 — 1 club, ≤25 members, attendance + leaderboard |
| **Pro** | Most clubs | ~$25–40 / month / club — unlimited members, chat, events, AI coach, custom subdomain, en/bn |
| **Enterprise** | Federations, larger organizations, clubs requiring SSO or data residency | Custom — SSO, audit, SLA, dedicated DB / region, custom domain |

**Self-funding threshold:** ~5–10 paying Pro clubs covers full infrastructure cost.

## Features

### Phase 0 — Enterprise Foundations (no user-facing features)

Multi-tenant data model · IaC · CI/CD · observability · audit logs · security baseline · Clerk + Stripe scaffolding. This phase is the platform that everything else stands on.

### Phase 1 — Attendance core (Tenant 0 only)

- Member directory with admin / member / guest roles, scoped to the tenant
- Weekly Sunday session view: RSVP (yes / no / maybe) + Zelle payment self-report (sender + reference)
- Admin one-click confirm payment → email notification to admin group
- Week navigator: arrows back/forward; future-day arrow greyed when at today
- Date label: "Wednesday, January 1, 2025"
- Per-tenant public leaderboard (sessions attended, times paid, last played)
- Announcements (admin-posted) + member comments
- Three sign-in methods: Google, email magic link, phone OTP (via Clerk)
- English + Bangla i18n
- Installable as a PWA (home-screen icon, web push)

### Phase 2 — SaaS commercialization

- Public signup → Stripe checkout → tenant auto-provisioning
- Tenant-admin console (manage members, settings, branding, billing)
- White-label: custom subdomain (`<tenant>.earlyeagles.club`), optional custom domain, logo, color theme, copy customization
- Platform-admin console (operator-only: see all tenants, impersonate for support, force resets)

### Phase 3 — Real-time chat

- Group channel + direct messages (SignalR + Azure SignalR Service)
- Image attachments
- Web push notifications, online presence, typing indicators

### Phase 4 — Events + guests

- Family picnic events with spouse / kids RSVP counts
- Fishing trips (men-only flag)
- Tournaments (round-robin / single-elim)
- Guest signup flow: a member invites a friend; guest gets an email account, can RSVP and pay
- Photo gallery per event

### Phase 5 — AI Coach

- Embedded coach persona — technique, tactics, drills, gear advice
- English with Bangla humor
- Short-first response style
- Per-tenant configurable (enable / disable, persona tone)

### Phase 6 — Mobile app

- Flutter or Expo (chosen at Phase 6)
- Reuses the same backend API + Clerk identity
- FCM push notifications

### Phase 7 — Enterprise tier

- B2B SSO (SAML / OIDC) via Clerk
- Advanced audit-log search and export
- SLA contracts (uptime, response time)
- Dedicated database / region option (data residency)
- SCIM user provisioning

### Phase 8 — Microsoft 365 Teams Agent (optional)

- Declarative agent / Copilot surface for clubs that already live in Teams. Reads from the same API; no parallel data store.

### Phase 9 — XR / MatchCourt (long-term)

- Unity + Meta XR SDK virtual clubhouse, remote rally simulation, AI-assisted coaching feedback. Authenticates via standard OIDC against Clerk.

## Out of scope (deliberate non-goals)

- **Online member-dues collection.** Stripe is used for tenant subscription billing only; member dues remain Zelle / e-Transfer / cash, tracked but not collected.
- **Generic tournament bracket engine.** Phase 4 supports round-robin and single-elim only.
- **Court / equipment booking with conflict resolution.**
- **Replacing WhatsApp for casual chat in Phase 1.** Phase 3 introduces in-app chat.
- **Public marketing SEO site.** A simple landing page ships in Phase 2; full marketing site is a separate concern.

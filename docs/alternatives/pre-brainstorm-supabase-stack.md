# Rejected alternative — Pre-BrainStorm Supabase quick stack

> **Status:** Not chosen. Recorded for posterity.
> **Date snapshot:** 2026-05-08 (before reading `BrainStorm/` and before the owner chose "full future-proof, no time pressure" mode).
> **Final decision:** See [`Plan.md`](../../Plan.md) and [`docs/adr/0001-stack-and-architecture.md`](../adr/0001-stack-and-architecture.md) (Alt A).

This document preserves the original technical recommendation made in the very first iteration of the conversation, before:

1. The seven `BrainStorm/` documents were read.
2. The owner clarified there is no time pressure.
3. The owner explicitly chose the "full future-proof" option.

It is kept here so that future contributors (and future-you) can see the alternative that was on the table, the case made for it, and the specific reasons it lost — without having to dig through chat logs.

---

## The stack that was recommended

| Layer | Pick |
|---|---|
| Domain | `earlyeagles.club` via Cloudflare Registrar (~$12/yr) |
| DNS | Cloudflare (free) |
| Frontend | Next.js (App Router) + TypeScript + Tailwind + shadcn/ui |
| Frontend hosting | Vercel Hobby (free) |
| **Backend (all-in-one BaaS)** | **Supabase** — Postgres + Auth + Realtime + Storage + Row-Level Security |
| Email | Resend (3K/month free) |
| Push | Web Push API (VAPID) |
| Mobile (v2) | Expo / React Native |
| **Total cost** | **~$12/year** |

The defining choice was **Supabase as a single backend-as-a-service** that bundled Postgres, authentication (Google + email + phone), realtime WebSockets, and object storage behind one dashboard and one set of credentials.

## The phasing that went with it

Originally framed assuming roughly full-time work pace:

- **v1.0** — Attendance core (~6–7 weeks)
- **v1.1** — Real-time chat using Supabase Realtime (~3–4 weeks)
- **v1.2** — Events: picnic + fishing + guest signup (~2 weeks)
- **v2** — Native iOS + Android via Expo (~2–3 months later)

Four phases total. No Phase 0 foundations work, no AI Coach phase, no Teams agent phase, no XR phase, no multi-tenant SaaS phase.

## Why this stack was attractive

- **One service for the entire backend.** Auth, Postgres, realtime WebSockets, image storage — all under one Supabase project. Drastically less moving parts to learn, deploy, monitor, and pay for.
- **Free tier covered club scale comfortably.** At ~35 users, the free quotas (500 MB DB, 200 concurrent realtime connections, 2 million realtime messages/month, 1 GB storage, 50K monthly active auth users) were nowhere near being hit.
- **Fastest path to a working product.** No backend code to write — Supabase auto-generates a typed client SDK from your Postgres schema. The developer writes only the frontend.
- **Realtime came essentially for free.** Phase 2 chat would be "subscribe to changes on the `messages` table" rather than building SignalR hubs.
- **Auth came essentially for free.** Supabase Auth supports Google, email magic link, and phone OTP out of the box.
- **Lowest cash cost.** ~$12/year — just the domain.

## Why it lost

The stack was right for the *original* framing of the problem. The framing changed in two ways during the conversation, and both changes invalidated key Supabase advantages:

| Original framing | Final framing | Effect on Supabase choice |
|---|---|---|
| Ship a working app to the club fast | No time pressure; quality and longevity over speed | The speed advantage of Supabase stopped mattering |
| Web app first; mobile maybe later | Web + mobile (Flutter or Expo) + optional Teams agent + long-term Unity/XR client, all consuming one API | Supabase Auth has no native Unity SDK — Phase 8 would require swapping auth providers |
| Single surface, tight coupling OK | API-first, many surfaces, no vendor lock-in | Supabase encourages frontend-direct-to-database access via RLS, which works against an API-first design |

Concretely, the deciding factors were:

1. **Unity / XR support.** Of the auth providers considered, only Firebase Auth has a first-party Unity SDK. Choosing Supabase Auth in Phase 0 would force an auth migration before Phase 8 could ship.
2. **API-first architecture conflict.** Supabase's natural pattern is Next.js Server Components calling the database directly via the client SDK. That works beautifully for a single-surface app and fights you when you have four future surfaces that all need to consume the same logic.
3. **Vendor lock-in.** Supabase couples auth, database, realtime, and storage to one vendor. Containerized ASP.NET Core + Postgres + Firebase Auth + Cloudflare R2 are independently swappable — important for a project meant to live for years.
4. **Owner explicitly chose "full future-proof."** Once that was the stated mode, the entire reason to pick Supabase (speed) was off the table.

## When this stack would have been the right answer

If any of the following were true, the recommendation would not have changed:

- A working app needed to ship in 4–6 weeks for a real deadline.
- Web was the only surface that would ever exist (no mobile, no XR, no agent).
- The developer had a strong preference against operating a backend container.
- Multi-tenant SaaS was permanently off the table.

None of those held. So the recommendation moved to ASP.NET Core + Firebase Auth + SignalR + Cloudflare R2 + PostgreSQL, documented in [ADR-0001](../adr/0001-stack-and-architecture.md).

## If this decision is ever revisited

Re-opening the Supabase choice should be done by writing **ADR-0002+**, not by editing ADR-0001 or this file. ADRs are immutable history. Triggers that *could* legitimately re-open it:

- Owner drops the XR ambition entirely (Phase 8 cancelled).
- Owner drops the mobile ambition entirely (Phase 5 cancelled).
- Owner shifts back to a "ship fast" mode for a specific deadline.
- Supabase ships first-party Unity support and a clearer API-first pattern.

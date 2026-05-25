# Member landing — `/` after login

This document captures the *visual shape and intent* of the member home page. The architectural decisions behind it are in [ADR-0004](../adr/0004-role-based-landing-and-routing.md) (route structure) and [ADR-0005](../adr/0005-light-social-layer.md) (the social-layer blocks). This file is the wireframe + behaviour notes the eventual implementer can build from.

**Implementation phase:** Phase 1 (after the Phase 0 backend foundation and the `Session` / `Rsvp` / `Attendance` aggregates exist).

## Design intent

A regular member opening the app should immediately feel four things:

1. **It's attractive.** Athletic-modern dark aesthetic (see [feedback memory](file:///c:/Users/skyli/.claude/projects/d--Projects-EarlyEagles/memory/feedback_frontend_design.md)) — bold typography, jersey numerals, Bangladesh-flag accent moments, photo thumbnails.
2. **It tells me what's going on.** Hero answers "am I playing this Sunday?" Below that, my dues are settled or not, my attendance percentage, the latest announcement.
3. **It's entertaining between Sundays.** The Club Wall photo strip and a chat preview give a reason to open the app on a Tuesday — not just on game day.
4. **It lets me act.** One-tap RSVP, react to photos and announcements, jump to the wall, open chat, upload a photo, RSVP to the next event — all reachable from one screen.

This is explicitly **not** a dashboard. The chart-heavy view from Phase 0 moves to `/insights` (admin-only) per ADR-0004. Members see a club bulletin board, not analytics.

## Wireframe

```
┌──────────────────────────────────────────────────────────┐
│  EARLY EAGLES                              Hello, Razi  │
│  ● LIVE · Sunday Edition · 25 MAY                       │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ───  THIS SUNDAY  ──────────────────────              │
│  31 MAY · 9–11am · Riverside Gym, Court 2               │
│                                                          │
│  ┌─────────────────────────────────────────┐            │
│  │   Are you in?                            │            │
│  │   [ ✓ I'M IN ]  [ ? MAYBE ]  [ ✗ OUT ]  │            │
│  └─────────────────────────────────────────┘            │
│  18 in · 4 maybe · 2 out · 11 unanswered                 │
│  [R][S][A][M][K] +13 →                                  │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  YOUR STATUS                                            │
│  Dues May  ✓ paid       Attendance Q2  9 / 12  ▲ 75%   │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ───  ANNOUNCEMENT  ─────────────────────              │
│  "Court 1 closed next week. We've moved to Court 3,     │
│   same time as always."                                  │
│         — Treasurer · 2 days ago                         │
│  ❤️ 5   🏸 3   👍 8   ·   4 comments  →                 │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ───  FROM THE CLUB  ──────────────   see wall →       │
│  [photo] [photo] [photo] [photo]                        │
│  "Picnic last weekend"  ·  Razi  ·  3d  ·  ❤️ 12        │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ───  CHAT  ──────────────────────   open chat →       │
│  Razi: "Anyone bringing extra shuttles?"                │
│  Sami: "I've got 3 tubes" · 12m ago                     │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ───  NEXT EVENT  ────────────────                      │
│  Summer Picnic · 14 June · 12 RSVPed · → RSVP           │
│                                                          │
└──────────────────────────────────────────────────────────┘
   [Home] [Wall] [Roster] [Events] [Chat] [Settings]
```

## Block-by-block behaviour

### Masthead + live strip

- Pulsing red dot + "LIVE · Sunday Edition" + today's date.
- Wordmark + logo slot (drop `apps/web/public/logo.png` to render).
- Right side: greeting + avatar dropdown (profile, settings, sign out).
- Bottom edge: green/red/grey accent strike — Bangladesh-flag rhythm.

### This Sunday (hero)

The single most important block. Most members open the app to interact with this block specifically.

- Session metadata: date, time, venue, court.
- **One-tap RSVP** with three states: In / Maybe / Out. Member's current state highlighted.
- Aggregate counts below the buttons (`18 in · 4 maybe · 2 out · 11 unanswered`).
- Avatar row of who's confirmed; "+13 →" expands to full roster preview.
- If the user has already RSVPed, the section subtly de-emphasises (shows current state, doesn't beg).
- Backed by Phase 1 `Session` + `Rsvp` aggregates.

### Your status

Glanceable, not analytical. **Not** the place for charts.

- Two paired chips: dues status (paid ✓ / unpaid amount) and quarter attendance (count + percentage + delta arrow).
- Tap → opens detail pages (`/dues`, `/attendance`) — both Phase 1 surfaces.
- Backed by Phase 1 `Payment` and `Attendance` aggregates.

### Announcement

Read-only here; composer is on `/admin`.

- Most recent admin announcement. Older announcements live at `/announcements`.
- Body, author, timestamp.
- **Reaction bar** (per ADR-0005, P3-019 + P4-026 primitive): the allowed emoji set, with counts.
- **Comment count + link** to the announcement detail page where the comment thread lives.
- One-line preview of the most-recent comment is acceptable if it fits.

### From the Club (photo wall preview)

The "entertainment" surface — the reason to open the app between Sundays.

- 4–6 most recent photos as a horizontal thumbnail strip.
- Below: most recent photo's caption + uploader + reactions.
- "see wall →" links to `/wall` (Phase 4, P4-022 / P4-023).
- The strip is *not* a carousel that auto-advances — chronological, scroll horizontally on touch.

### Chat preview

A nudge, not the full chat.

- Last 2–3 messages from the default tenant channel (`general`).
- Author + message + timestamp.
- "open chat →" links to `/chat` (Phase 3).
- Unread count badge if the member hasn't read recent messages.

### Next event

- Title, date, RSVP count, primary action.
- If member has already RSVPed, action becomes "✓ You're going" with a way to change.
- Backed by Phase 4 `Event` aggregate.

### Bottom navigation

Mobile-first nav bar (PWA-installable).

- Home (this page)
- Wall (Phase 4 photo wall, plus event-attached galleries)
- Roster (members directory)
- Events (upcoming + past)
- Chat (Phase 3)
- Settings (profile, notifications, dues history)

Admin users see the same nav plus a section divider and the admin routes (`/admin`, `/insights`) — per ADR-0004 they don't land here, but they can navigate to the member view.

## Aesthetic locks (from the feedback memory)

- **Palette:** dark by default. Court-black ground, steel-white text, Bangladesh green primary, Bangladesh red as the live-pulse / destructive accent, shuttle-yellow sparingly. CSS tokens already in [`apps/web/app/globals.css`](../../apps/web/app/globals.css).
- **Typography:** Big Shoulders Display (variable, jersey energy) for headlines + stat figures; Hanken Grotesk for body. Already loaded via [`apps/web/app/layout.tsx`](../../apps/web/app/layout.tsx).
- **Motion:** entrance reveal via Motion's RevealStack (already in `apps/web/components/editorial/reveal.tsx`), live pulse on the LIVE indicator, subtle staggered card appearance. Respects `prefers-reduced-motion`.
- **No editorial-cream / serif / claret** — that direction was abandoned 2026-05-24.

## Tasks this implementation touches

Mostly Phase 1 (`Session`, `Rsvp`, `Attendance`, `Payment` domain + frontend), with Phase 3 and Phase 4 contributing the social-layer blocks:

| Block | Primary backing tasks |
|---|---|
| This Sunday hero | Phase 1: `Session`, `Rsvp` domain + RSVP endpoint + frontend |
| Your status | Phase 1: `Payment` + `Attendance` summary endpoints |
| Announcement | Phase 1: `Announcement` aggregate + frontend; reactions/comments per `P3-019`, `P4-027`–`P4-029` |
| Club wall preview | Phase 4: `P4-022` (feed endpoint) + `P4-023` (feed UI extracted as embeddable strip) |
| Chat preview | Phase 3: last-messages endpoint, embeddable preview component |
| Next event | Phase 4: `Event` aggregate + RSVP |

When Phase 1 starts, the first task is the bare member `/` (hero + status only, the social blocks empty-state until their phases land). As Phase 3 and Phase 4 ship, blocks light up — they should not be gated behind a feature flag from the member's perspective; just appear when their data is available.

## Open questions for future ADRs (don't decide now)

- **Empty-state copy.** "No photos yet. Be the first to share." vs. dropping the block entirely until there's content. Recommendation: keep the block with empty-state, with a call to upload — invites first content.
- **Streaks and gamification.** Should "Attendance Q2 9/12" become a streak badge ("4-week streak ▲") for stickiness? Probably yes for Phase 5 — but it's not in ADR-0005's social-layer scope, so defer to a future ADR.
- **Personal feed vs club-wide feed.** Currently the wall is club-wide chronological. Could later filter to "people you played with" — premature for Tenant 0 at 35 members; revisit when a tenant has 200+ members.
- **Push-notification cadence per block.** Already scoped in ADR-0005 ("not on every reaction"), but the per-block specifics get decided at implementation time.

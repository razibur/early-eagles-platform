# ADR-0014: Add a play layer (game rotation, scores, ratings) and small member extras

- **Status:** Accepted (rotation part superseded by [ADR-0015](./0015-court-groups-set-by-admin.md))
- **Date:** 2026-11-25
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) appendix "Play layer and member extras (2026-11-25)", P4-033 to P4-052, [ADR-0004](./0004-role-based-landing-and-routing.md), [ADR-0005](./0005-light-social-layer.md)

## Context

The planned features are mostly club admin: sessions, RSVPs, payments, announcements, events.
Chat (Phase 3) and the light social layer (ADR-0005) give members some reasons to open the app
between Sundays. Nothing in the plan helps with **the games themselves**: on Sunday the club
still works out doubles pairings and whose turn it is by shouting across the hall.

The owner asked which other features would make the app attractive to members. Ranked by how
much the members would use them, the answer was:

1. Sunday game rotation: fair doubles pairings and a "who plays next" queue for the people
   at the hall.
2. Score logging, a per-player rating and partner / head-to-head stats.
3. Badges and streaks for attendance and paying on time.
4. Small extras: an RSVP waitlist when the courts are full, polls, a shared-cost tracker,
   a carpool board, birthday and milestone shout-outs.

Items 1 and 2 also matter for selling the product. Most club apps only cover admin work, so
pairing players and tracking results is what would set this one apart for other clubs.

Constraints that still apply: every entity carries `TenantId` and is covered by the isolation
gate (P0-034); every state change writes an `AuditEvent`; every capability ships behind a flag,
off by default in production; tasks are 1–3 hours; the cost posture is as free as possible
(none of this needs a paid service).

## Decision

Add two task blocks to Phase 4, recorded in the Implementation-Plan appendix
"Play layer and member extras (2026-11-25)":

**Play layer (P4-033 to P4-044).**

- A `Match` entity (tenant, session, optional event, court number, two teams of one or two
  members, status Queued / Playing / Finished) with a `MatchScore` per game. Phase 4
  tournaments (P4-005, P4-006) use the same `Match`, so there is one match model.
- **Rotation.** The admin, or any member the admin trusts, marks who is at the hall. The app
  generates the next round of doubles across the club's courts. It favours people who have sat
  out longest and avoids repeating partners. Later it can also balance teams by rating.
- **Scores.** Either team enters a game score on a phone at the court. The other team or an
  admin can correct it. Every change is audited.
- **Rating.** One doubles rating per member per club, updated after each finished match with a
  simple Elo-style formula. **Members can hide their rating from others** (it still drives
  balancing). The club leaderboard shows only the ratings members have chosen to show.
- **Stats.** Win / loss record, best partner, head-to-head.
- **Badges and streaks.** A fixed set worked out from data the app already has (consecutive
  Sundays, total sessions, paid on time, matches played). Tenants cannot define their own
  badges in this phase.

**Member extras (P4-045 to P4-052).**

- **Waitlist.** An optional session capacity. RSVPs over capacity join a waitlist and are moved
  up in order when someone drops out, with a push notification.
- **Polls.** Admin-created, single or multiple choice, with a closing time. They live next to
  announcements.
- **Shared costs.** Record who paid for shuttles, a court deposit and so on, and how the cost
  splits. **Tracking only**: like dues, no money moves through the app.
- **Carpool board.** Members offer or claim seats for a session. It closes when the session
  starts.
- **Shout-outs.** An optional birthday (month and day only, not the year) and automatic
  milestone posts ("50th session") on the club wall.

Already planned, so not added again: calendar export (P4-014), event reminders (P4-013), push
notifications (Phase 1), chat (Phase 3).

**Timing.** Both blocks sit in Phase 4 so they share the match model with tournaments. Rotation
and scores (P4-033 to P4-038) depend only on Phase 1 sessions and RSVPs. They can be moved
forward to straight after Phase 1 if the owner wants them sooner; that is an ordering change,
not a new decision.

## Consequences

**Positive**

- Members get a reason to open the app *during* play, not just to RSVP or pay.
- A feature other clubs would pay for, which most rivals don't offer.
- One `Match` model covers casual rotation and tournaments.
- No new paid service or infrastructure.

**Negative / costs**

- About 20 more tasks in Phase 4 (32 becomes about 52).
- Ratings can hurt feelings in a social club of middle-aged players. Mitigated by the
  hide-my-rating setting and by keeping the leaderboard opt-in.
- Entering scores at the court has to take under 10 seconds, or nobody will do it. The UI needs
  a design pass (frontend-design skill) before it is built.
- A birthday is personal data: it is optional, stored without the year, and must be removed
  when the member is removed from the club.

**Follow-ups**

- P4-033 to P4-052 added in the Implementation-Plan appendix.
- P4-005 / P4-006 (tournament generators) build on the `Match` entity from P4-033.
- PROGRESS.md Phase 4 line updated with the new task count.

## Alternatives considered

| Option | Why not |
|---|---|
| Leave the plan as it is (club admin + chat + social layer) | Members would still sort out games on paper or by shouting, and the product would have nothing that sets it apart |
| Online dues payment from the member page | Out of scope by design (project-scope.md): fees, refunds, payout and money-transmitter rules. Members get a "Pay $15" card with the Zelle details to copy and an "I paid" button (Phase 1) |
| A full ladder / league engine with seasons and divisions | Too big and too specific; casual rotation plus a rating covers what a weekly social club needs. Revisit if a competitive tenant asks |
| Court booking with conflict resolution | Still out of scope. Rotation assigns players to courts the club already has; it does not book courts |
| Tenant-defined custom badges | Adds an admin builder for little gain; a fixed set first, custom badges in Phase 7+ if asked |

## Revisit triggers

- Members of Tenant 0 don't enter scores after the first month in use (rethink the score-entry
  UI, or drop ratings and keep only rotation).
- A tenant asks for leagues, seasons or divisions.
- Complaints about ratings, which would mean hiding them by default.

# ADR-0015: Court groups set by the admin before the session, instead of a live rotation queue

- **Status:** Accepted (supersedes the rotation part of [ADR-0014](./0014-play-layer-and-member-extras.md))
- **Date:** 2026-11-26
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) appendix "Court groups (2026-11-26)", P4-053 to P4-060, [ADR-0004](./0004-role-based-landing-and-routing.md), [ADR-0014](./0014-play-layer-and-member-extras.md)

## Context

ADR-0014 planned a live rotation: the app generates each round of doubles across all courts at
the hall and keeps a "who plays next" queue. The owner then described how Early Eagles actually
plays:

- **A court holds at most 6 players** (4 play doubles, 2 wait). The club books courts at the
  venue based on how many people are coming.
- **Extra players don't add a court.** If 31 come, the club books 5 courts and the 1 extra
  joins whichever court has a free spot.
- **One court is always booked**, even if fewer than 6 say Yes.
- **The admin decides who is on which court.** The app can suggest; the admin has the final say.
- **Within a court, players organise their own games.** Players may move to another court at
  the hall to play a different team. That is optional and informal.

What members want to see when they open the app is **this week's courts and who is on each**.
A round-by-round queue they would mostly ignore at the hall.

## Decision

**Courts to book.** For a session, `courts = max(1, floor(yes / maxPerCourt))`, with
`maxPerCourt` a tenant setting (default 6). The admin home shows "Book N courts" from the live
Yes count. Booking at the venue stays outside the app (court booking is out of scope).

**Court groups.** A `CourtAssignment` per session: court number → up to `maxPerCourt` members,
plus a **flex** list for the players left over (`yes − courts × maxPerCourt`). Flex players are
shown as "play on any court with a free spot". Status Draft → Published.

**Who assigns.** The app makes a draft (spread players evenly, and avoid repeating last week's
groups). The admin moves people, marks flex players and publishes. Publishing sends a push:
"You're on Court 3". Every change is audited.

**What members see.** On the member home (`/`), a top-down picture of this week's courts with
names on each and the member's own court highlighted, plus the flex list. No game order, no
queue.

**Late changes.** An RSVP change after publishing doesn't reshuffle anything automatically. The
admin is told, edits the groups and republishes; only the players whose court changed get a
push.

**Moving courts at the hall** is not tracked. The published groups are a starting point.

**What stays from ADR-0014:** score entry, ratings, hide-my-rating, stats, badges and all the
member extras. Score entry no longer depends on rotation: a member records a match by picking
the 2–4 players and the score.

**Dropped from ADR-0014:** the live rotation generator, the "who plays next" queue,
round-by-round matches and "balance by rating" for rounds (P4-035 to P4-037, P4-039, P4-042).
Balancing by rating moves to the court-group draft, once ratings exist.

**Timing.** P4-053 to P4-058 depend only on Phase 1 sessions and RSVPs. The court picture is the
most visible member feature in the plan, so it can be moved to straight after Phase 1 without a
new decision.

## Consequences

**Positive**

- Matches how the club already plays; nothing new to learn at the hall.
- A strong reason for members to open the app before every Sunday.
- Simpler than ADR-0014's rotation: no round state to keep in step with what happens at the hall.
- The admin stays in control (friends together, new players next to helpful regulars).

**Negative / costs**

- Some admin work each week (reviewing and publishing). Kept small by the automatic draft.
- The app doesn't know who actually played where, so score entry relies on members picking the
  right players.
- With many flex players (e.g. 35 Yes → 5 courts, 5 flex) courts get crowded. That is how the
  club plays today; the admin can book an extra court at the venue and add it in the app.

**Follow-ups**

- P4-053 to P4-060 added in the Implementation-Plan appendix; P4-035 to P4-037, P4-039 and P4-042
  marked dropped; P4-034 and P4-038 reworded.
- ADR-0014 status line marked "partly superseded by ADR-0015".

## Alternatives considered

| Option | Why not |
|---|---|
| Keep the live rotation queue from ADR-0014 | Not how the club plays; players sort out games themselves on their court |
| `courts = ceil(yes / 6)` (add a court for any extras) | The owner's rule: extras join any court with space; courts are booked in full groups |
| Fully automatic assignment, no admin step | The owner wants the admin to decide |
| Fixed game order within a court | Players organise their own games; the app only shows who is on the court |
| Track players moving courts at the hall | Optional and informal; tracking it would add taps nobody makes |

## Revisit triggers

- A tenant plays a different format (e.g. 4 per court, singles, or timed rounds).
- The admin finds the weekly review a chore (make the draft publish automatically at the cutoff).
- Members want to record moving courts.

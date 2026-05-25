# ADR-0004: Role-based landing pages and route structure

- **Status:** Accepted
- **Date:** 2026-05-25
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Plan.md`](../../Plan.md), [`Implementation-Plan.md`](../../Implementation-Plan.md), [`PROGRESS.md`](../../PROGRESS.md), [ADR-0002](./0002-enterprise-saas-reframe.md)

## Context

Phase 0 shipped a chart-heavy dashboard at `/` (attendance bars, dues lines, member-growth area, summary stats). It is well-built for what it is — but it is **the wrong landing page for a regular club member**. A member opening the app on Sunday morning is asking *"am I playing today?"*, not *"how have we been trending across twelve weeks?"*. The chart dashboard is an **analytics view**, appropriate for the tenant-admin (treasurer, organiser), not for the majority of users who are players.

Multi-tenant B2B SaaS apps in this space (Meetup organiser vs attendee, Eventbrite organiser vs attendee, Discord owner vs member, sports-league apps) consistently differentiate the post-login surface by role. The decision below codifies that pattern for EarlyEagles.

A separate question — should the same URL serve role-aware content, or should each role have its own route? — was put to the project owner. They selected separate routes so that admin surfaces can be locked behind middleware cleanly, and so the URL itself communicates which audience a deeplink targets.

## Decision

Three top-level routes are reserved, role-gated by Clerk org-role:

| Path | Role required | Purpose |
|---|---|---|
| `/` | any signed-in member of the tenant | **Member home.** Hero RSVP card for the next session, my dues status, my recent attendance, latest announcement (read-only), next event, and (from Phase 4) a Club Wall preview. |
| `/admin` | `org:tenant-admin` | **Admin ops home.** Full RSVP grid for the next session, unpaid-dues delinquency list, announcement composer, quick-action shortcuts (mark attendance, log payment, send reminder). |
| `/insights` | `org:tenant-admin` | **Analytics surface.** The current chart dashboard — attendance over time, dues by method, member growth, KPI tiles. Moved here from `/` as part of Phase 1. |

Shared surfaces (`/roster`, `/events`, `/chat`, `/settings`) render role-aware content but live at one path each.

### Login flow

1. Clerk authenticates the user and redirects to `/`.
2. `apps/web/middleware.ts` reads the Clerk session and the active org-role claim.
3. If the role is `org:tenant-admin`, the middleware redirects to `/admin`. Otherwise the request continues to `/`.
4. Any request to `/admin` or `/insights` without the admin role returns 403 (or silently redirects to `/`, decided at implementation time).

### Implementation timing

This ADR documents the intent. **The implementation lands in Phase 1**, not now:

- Member `/` (RSVP-first) requires the `Session`, `Rsvp`, `Attendance` aggregates and their API endpoints — all Phase 1 work.
- Admin `/admin` requires the same domain plus the announcement composer.
- The current chart dashboard **stays at `/` as a Phase 0 placeholder** until the Phase 1 surfaces ship, at which point it moves to `/insights` in a single Phase 1 task.
- Clerk middleware lands as part of P0-039 (frontend Clerk SDK).

## Consequences

### Saved
- Deeplinks in emails / SMS / push become role-honest (a link to the admin dues view sent to a member would 403 cleanly, not show a confusing empty page).
- Admin route gating is one middleware function, easy to audit.
- The investment in the chart dashboard is preserved — only its position changes.

### Cost
- Sharing a URL between an admin and a member ("look at this") now requires the link target to match the recipient's role, or the recipient sees a 403.
- Two landing pages must be maintained instead of one.
- Future role expansion (e.g., `coach`, `parent-guardian`) would each likely want its own landing — sets a precedent for proliferation.

### Reversibility
This ADR can be superseded by a future "role-aware single landing at `/`" decision without data migration — just remove the middleware redirect and the `/admin` route, render conditionally at `/`. Low cost to reverse.

### Open items
- Exact 403 behaviour (hard 403 page vs silent redirect to `/`) — decided at Phase 1 implementation time.
- Whether the admin's `/admin` should show a "view as member" toggle for empathy / debugging — out of scope, defer.

# Glossary (P0-099)

The words the code, the docs and the club use, with one meaning each. When a term here and a
name in the code disagree, fix one of them, then this file.

## The club side

**Tenant.** One club using the platform, e.g. Early Eagles. Everything a club owns (members,
sessions, payments, audit history) carries its `tenant_id`, and the EF Core global query filter
keeps every read inside one tenant. In code: `Tenant` (`tenants` table). In Clerk: an
*Organization* (P0-035). *Not* the same as a member.

**Tenant 0.** The Early Eagles club: the first real tenant, used to prove every feature before
paying clubs see it.

**Slug.** A tenant's short, URL-safe name, which is also its subdomain: `early-eagles` →
`early-eagles.earlyeagles.club`. Lowercase, unique, resolved from the host on every request by the
API (`SubdomainExtractor`) and the web app (`proxy.ts`).

**Member.** A person who belongs to a tenant. One person can be a member of several clubs: same
sign-in, separate memberships. In code: `TenantMember` (`tenant_members`, keyed by tenant and Clerk
user id), with a **role**.

**Role.** What a member may do in their club. `Member`: plays, RSVPs, sees their own dues.
`TenantAdmin`: runs the club (organiser, treasurer), sees everyone's dues, the audit log and the
admin views. *Guest* (Phase 4): a non-member invited for one session or event.

**Session.** One scheduled block of court time the club plays, e.g. *Sunday 7–10 am*. Members RSVP to
it and attendance is taken against it. Phase 1 (`Session` aggregate, P1-005).

**RSVP.** A member's answer for a session before it happens: **yes**, **no** or **maybe**. Planning,
not proof: the organiser books courts and shuttles from RSVPs. Phase 1.

**Attendance.** Who actually played at a session, recorded by an admin on the day. Feeds the
leaderboard and dues. Differs from RSVP: a *yes* who didn't come is an RSVP without attendance.
Phase 1.

**Dues.** What a member owes the club for playing, e.g. $15 a Sunday at Early Eagles. Paid outside
the platform (Zelle, e-Transfer, cash), **tracked, never collected**: a member self-reports a
payment (sender + reference), and an admin confirms it (`PaymentConfirmed`). Never confused with the
club's own subscription.

**Leaderboard.** A tenant's public ranking by sessions attended, times paid and last played.
Phase 1.

**Event.** Something other than a regular session: a picnic, fishing trip, tournament, with its own
RSVP (spouse/kids counts, guests). Phase 4.

## The platform side

**Plan.** What a tenant pays the platform: `Free`, `Pro` or `Enterprise` (`TenantPlan`), billed
through Stripe. This is the *only* money the platform collects.

**Tenant status.** `Active`, `PastDue` (payment failed), `Suspended`, or `Deactivated` (terminal)
(`TenantStatus`).

**Platform admin.** The operator of the whole platform (Phase 2 console). Not a role inside any
tenant.

**Tenant context.** The tenant a request is for, resolved once per request from the subdomain
(`ITenantContext`). Unresolved means *no tenant*, and tenant-scoped reads return nothing.

**Isolation gate.** The integration tests proving one tenant can never read or write another's
data (P0-034, `EarlyEagles.Tests.Integration`). Must stay green before anything merges.

**Audit event.** One append-only row (`audit_events`) recording an attempted state change: what, by
whom, when, and whether it succeeded. Written by `AuditBehavior` for every Mediator *command*; the
database rejects updates and deletes.

**Command / query.** A Mediator message that changes state (command: audited, run in a transaction)
or only reads it (query: neither). Example query: `ListAuditEventsQuery`.

**Domain event.** A fact a change raised, e.g. *payment confirmed*. Stored in the **outbox**
(`outbox_messages`) in the same transaction as the change, then published by the **outbox processor**
to the Service Bus topic `domain-events`. Consumers subscribe by **subject** (the short event name,
e.g. `PaymentConfirmed`).

**Feature flag.** A per-tenant switch (Flagsmith) that every new user-facing capability ships behind,
off by default in production.

**Environment.** Where the platform runs: **dev** (your machine), **sandbox** (throwaway Azure copy),
**staging** (on demand), **prod**.

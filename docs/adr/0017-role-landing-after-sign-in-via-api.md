# ADR-0017: Admins are sent to /admin once, after sign-in, using the API's answer for the role

- **Status:** Accepted
- **Date:** 2026-11-26
- **Deciders:** Project owner (Moh. Razib)
- **Refines:** [ADR-0004](./0004-role-based-landing-and-routing.md) (login flow and open items).
  ADR-0004's routes (`/`, `/admin`, `/insights`) and its intent stand.
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) P1-035;
  [`docs/developer-walkthrough.md`](../developer-walkthrough.md) §4.11

## Context

ADR-0004's login flow has the web app's middleware (now `proxy.ts`) read the Clerk session's
organization role and redirect `org:admin` from `/` to `/admin`. Building it (P1-035) showed three
problems:

1. **The token can't say whether the role is for this club.** The token carries the user's
   *active* Clerk organization and their role in it. Whether that organization is the club at this
   address is known only to the API, which compares it with the tenant's `ClerkOrganizationId`
   ([`TenantRoleClaimsTransformation`](../../apps/api/src/EarlyEagles.Api/Authentication/TenantRoleClaimsTransformation.cs)).
   The web app has no tenant table. An admin of one club visiting another would be sent to an admin
   page that refuses them.
2. **Admins are players too.** A redirect on every visit to `/` would stop an admin from ever
   seeing the member home, where they answer RSVPs and report their own dues.
3. ADR-0004 left open whether a non-admin on `/admin` gets a 403 page or a redirect.

## Decision

- **After sign-in only.** Clerk's fallback redirect after sign-in and sign-up is `/welcome`
  ([`AFTER_AUTH_PATH`](../../apps/web/lib/auth/routes.ts)). That page asks the API's `/me` for the
  role in this club, then *replaces* itself (so Back skips it): `tenant-admin` goes to `/admin`;
  anyone else, and any error, goes to `/`. A sign-in that already carries a `redirect_url` (for
  example after following a deep link) goes straight back to that link.
- **`/` is never redirected.** Admins reach `/admin` from a **Club desk** link in the account menu,
  shown only when `/me` says `tenant-admin`.
- **A non-admin on `/admin` sees a message, not a redirect** ("This page is for the club's
  admins."), and the page requests no admin data. The security boundary stays in the API, which
  answers 403 to every admin endpoint whatever the browser shows.
- Signing out clears the browser's cached API answers, so the next person in the tab never sees the
  previous person's role.

## Consequences

- The role decision has one source, the API. The web app never reimplements "is this organization
  this club?".
- Sign-in costs one extra hop: a brief "Opening your club…" page and one `/me` call.
- `/admin` and `/insights` aren't gated in `proxy.ts`. A non-admin who types the address gets the
  page shell with the message, never data. If deep links to admin pages become common, a server-side
  gate can be added later by having the proxy ask the API, without changing this flow.
- Reversible at low cost: change `AFTER_AUTH_PATH` back to `/` and delete `/welcome`.

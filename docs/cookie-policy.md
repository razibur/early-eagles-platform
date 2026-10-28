# Cookie policy — inventory and review notes (P0-076.2)

> **Stub, not legal advice.** This file is the maintained inventory behind the published policy
> page ([`apps/web/app/cookies/page.tsx`](../apps/web/app/cookies/page.tsx), served at `/cookies`).
> Before the platform is sold to other clubs, have it reviewed for the jurisdictions tenants
> operate in (US states first; EU/UK if a tenant is there).

## Rules

1. **Necessary** storage is set without asking: it is needed for the app to work or to remember
   the visitor's own choices.
2. **Optional** storage (today: analytics) is off until the visitor opts in through the banner
   (`ee_consent`), and off again if they withdraw. Code that sets optional storage must check
   `useConsentStore().analytics === true` first.
3. Refusing is as easy as accepting: the banner shows *Necessary only* next to *Accept analytics*,
   and the footer's *Cookie settings* reopens it at any time.
4. Every new cookie or storage key is added to the table below **and** to the page, in the same
   change.

## Inventory

| Name | Kind | Category | Set by | Lifetime | Purpose |
|---|---|---|---|---|---|
| `ee_consent` | cookie (first-party, `SameSite=Lax`, `Secure` on HTTPS) | Necessary | web app (P0-072) | 365 days | Records the visitor's consent choice and when it was made |
| `ee-theme` | `localStorage` | Necessary | web app (P0-070.3) | until cleared | Light / dark / system display preference |

### Planned — add when the task lands

| Name | Category | Task | Notes |
|---|---|---|---|
| Clerk session cookies (`__session`, `__client_uat`, …) | Necessary | P0-039 | authentication; exact names from Clerk's docs at implementation time |
| PostHog (`ph_*` cookie / `localStorage`) | **Optional** (analytics) | P0-074 | load only after `analytics === true`; consider cookieless mode |
| Sentry | none expected | P0-073 | error reporting sets no cookies by default; confirm |

## Open questions for review

- Whether anonymous, cookieless analytics needs consent at all for the target jurisdictions
  (would allow PostHog's cookieless mode without the banner gate).
- Retention of `ee_consent` (365 days) versus re-asking periodically.
- Per-tenant policy text once tenants can add their own integrations (Phase 2).

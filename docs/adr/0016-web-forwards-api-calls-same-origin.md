# ADR-0016: The web app forwards API calls same-origin; the API trusts the forwarded host only with a shared key

- **Status:** Accepted
- **Date:** 2026-11-26
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) P0-071.2 to P0-071.5;
  [`docs/developer-walkthrough.md`](../developer-walkthrough.md) §4.12

## Context

The API works out the club (tenant) from the request's host name only: `alpha.earlyeagles.club` →
`alpha` ([`TenantResolutionMiddleware`](../../apps/api/src/EarlyEagles.Api/Tenancy/TenantResolutionMiddleware.cs),
P0-031). In production the web app runs on Vercel and the API on Azure Container Apps (ADR-0002), on
different hosts. When the browser on `alpha.earlyeagles.club` needs data, something has to carry
"alpha" to the API.

Options weighed (2026-11-26):

1. **Same-origin forwarding.** The browser calls `/api/*` on the club's own address; the web app
   forwards to the API and says which address was used in `X-Forwarded-Host`. The API believes that
   header only when the request also carries a key shared with the web app.
2. **Per-club API host.** The browser calls `alpha.api.earlyeagles.club` directly: wildcard DNS and
   certificate on Container Apps, CORS for every club origin, a second host shape in the subdomain
   parser.
3. **Tenant header.** The browser calls one API host with `X-Tenant-Slug`. Any client could claim
   any club; roles are still checked against the Clerk organization, but anything anonymous would
   trust the header.

## Decision

**Option 1.**

- The web app's `proxy.ts` forwards `/api/<path>` to `<API origin>/<path>`, keeping the browser's
  `Authorization` header and adding `X-Forwarded-Host: <the address the browser called>` and
  `X-EE-Proxy-Key: <shared key>` (P0-071.5).
- The API's [`ForwardedTenantHostMiddleware`](../../apps/api/src/EarlyEagles.Api/Tenancy/ForwardedTenantHostMiddleware.cs),
  just before tenant resolution, compares the key in constant time. A match plus exactly one valid DNS
  host replaces `Request.Host`; anything else leaves the request with its own `Host`. The key header
  is removed from every request so nothing downstream logs it (P0-071.4).
- The key (`TrustedProxy:Key`, at least 32 characters) is a secret: Key Vault for the API, the Vercel
  project's environment for the web app, user-secrets and `.env.local` on a laptop. Unset means no
  forwarded host is ever believed.
- The API keeps answering requests made directly on a club host, so tests, tools and a later direct
  path keep working.

## Consequences

- **No CORS.** The browser only ever talks to its own origin; Clerk's session cookie and the bearer
  token stay first-party.
- **The club stays tied to the address.** A browser can't pick another club by sending a header:
  Next overwrites `X-Forwarded-Host`, and without the key the API ignores it.
- **One extra hop** (Vercel → Container Apps) on every API call. Acceptable at this scale; revisit if
  latency shows up in traces.
- **A secret to rotate** in two places. Rotation means a short window where both sides must change;
  supporting two keys at once is a later refinement if needed.
- **The API must not be served at `api.earlyeagles.club`**, or any single label under a club domain:
  the subdomain parser would read it as a club called `api`. Use the Container Apps name or a host
  outside the club domains.
- Webhooks are unaffected: Clerk and Stripe call the API's own host and carry their tenant in the
  payload.

## Revisit triggers

- Latency from the extra hop matters, or the web app moves off Vercel next to the API (then a private
  network or `ForwardedHeaders` with known proxies can replace the key).
- A non-browser client (mobile app, integration) needs the API directly: option 2 becomes worth its
  infrastructure.

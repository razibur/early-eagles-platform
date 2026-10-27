# ADR-0011: Hand-written service worker instead of `next-pwa`

- **Status:** Accepted (supersedes the `next-pwa` pick in [ADR-0002](./0002-enterprise-saas-reframe.md)'s frontend row and [`Plan.md`](../../Plan.md)'s PWA row)
- **Date:** 2026-10-26
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) P0-067 / P0-068 / P1-023, `apps/web/node_modules/next/dist/docs/01-app/02-guides/progressive-web-apps.md`

## Context

The stack table names `next-pwa` for the PWA layer, and P0-067.1 says "install `next-pwa`".
Checked on 2026-10-26:

- **`next-pwa`** (5.6.0) was last published in August 2022. It wraps Workbox through a
  **webpack** plugin, and Next.js 16 builds with **Turbopack** by default.
- **`@ducanh2912/next-pwa`**, the usual fork, was last published in September 2024 and also
  needs webpack. Its author moved on to Serwist.
- **Serwist** (`@serwist/next`, `@serwist/turbopack` 9.5.x) is maintained and published in
  October 2026. It exists to generate **precaching / offline** service workers.
- The Next.js 16 PWA guide builds a PWA with **no plugin**: `app/manifest.ts` for the manifest,
  and a plain `public/sw.js` that the page registers. It suggests Serwist only for offline
  support, and notes that this "currently requires webpack configuration".

What Phase 0 needs from the PWA layer: an installable app (manifest + HTTPS + a registered
service worker; P0-067) and **Web Push** (P0-068). Offline-first data is Phase 6 (native app,
[`Plan.md`](../../Plan.md)). None of the Phase 0–2 work needs offline caching. A caching
service worker would also be a liability before then. It can serve stale tenant pages
after a deploy, and every cached route becomes something to invalidate.

## Decision

- **No PWA plugin.** The service worker is a hand-written `apps/web/public/sw.js`, served
  as-is, registered by a small Client Component in the root layout (`scope: '/'`,
  `updateViaCache: 'none'`). `next.config.ts` serves it with `Cache-Control: no-cache` and a
  `self`-only CSP, as the Next.js guide does.
- The worker has **no `fetch` handler and caches nothing.** It takes over immediately on
  activation and does nothing else until P0-068.2 adds the `push` / `notificationclick`
  handlers.
- The manifest uses the built-in **`app/manifest.ts`** metadata route (P0-067.2) rather than a
  hand-maintained `public/manifest.webmanifest`.
- **Zero new dependencies.**

## Consequences

- No offline support. A user without a connection sees the browser's offline page. That's
  acceptable for a club app used at the venue or at home, and it's the Phase 6 scope.
- If offline caching is wanted earlier, adopt **Serwist** in a new ADR. Use `@serwist/turbopack`
  if it has left experimental status by then, otherwise `@serwist/next` with the build switched
  to webpack. The service worker then gains precache entries; the registration component stays.
- Service-worker updates are our job: `updateViaCache: 'none'` + `no-cache` on `/sw.js` make the
  browser re-fetch it on every navigation check, and `skipWaiting` / `clients.claim` activate a
  new version without a second reload. With no caches, an immediate takeover can't serve a
  mismatched page.
- P0-067.1 and P0-067.2 are reworded in the Implementation-Plan appendix "PWA without a plugin
  (2026-10-26)". Their acceptance criteria stay the same.

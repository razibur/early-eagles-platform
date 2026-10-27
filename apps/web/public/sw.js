// Early Eagles service worker (P0-067.1, ADR-0011).
//
// Hand-written and served as-is from /sw.js: no plugin, no build step.
// Deliberately caches nothing and has no fetch handler, so every request
// goes to the network and a deploy can never be masked by a stale cache.
// Push / notificationclick handlers arrive with P0-068.2.

// Activate a new version straight away instead of waiting for every tab
// to close. Safe because there are no caches to fall out of step with.
self.addEventListener("install", () => {
  self.skipWaiting()
})

// Take control of pages opened before this worker activated, so the first
// visit is controlled without a reload.
self.addEventListener("activate", (event) => {
  event.waitUntil(self.clients.claim())
})

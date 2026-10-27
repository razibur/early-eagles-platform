"use client"

import { useEffect } from "react"

// Registers /sw.js once per page load (P0-067.1, ADR-0011). Renders nothing.
// `updateViaCache: "none"` makes the browser bypass its HTTP cache when it
// checks for a new worker, so a deploy is picked up on the next navigation.
export function ServiceWorkerRegistration() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return

    navigator.serviceWorker
      .register("/sw.js", { scope: "/", updateViaCache: "none" })
      .catch((error: unknown) => {
        console.error("Service worker registration failed", error)
      })
  }, [])

  return null
}

"use client"

import { useEffect } from "react"

import { useConsentStore } from "@/lib/stores/consent-store"

// Reads the consent cookie into the store once, after the first client
// render (the store uses skipHydration). Renders nothing.
export function ConsentHydrator() {
  useEffect(() => {
    void useConsentStore.persist.rehydrate()
  }, [])
  return null
}

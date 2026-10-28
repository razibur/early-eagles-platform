import { useSyncExternalStore } from "react"
import { create } from "zustand"
import { createJSONStorage, persist } from "zustand/middleware"

import { cookieStorage } from "@/lib/stores/cookie-storage"

// Cookie consent (P0-072 store, P0-076.1 banner). The first Zustand store:
// client-only state that several components share — the banner writes it,
// the footer's "Cookie settings" link reopens it, and analytics (PostHog,
// P0-074) will read `analytics` before loading anything.
//
// Persisted to a first-party `ee_consent` cookie for a year. Only the
// decision is persisted; whether the preferences panel is open is per page
// view. `skipHydration`: the store starts at the defaults on server and
// client alike, and ConsentHydrator rehydrates after mount, so the first
// client render matches the static HTML.
//
// Hydration is tracked through persist's own hasHydrated/onFinishHydration
// (useConsentHydrated), never as store state: every set() on a persisted
// store writes the cookie, and a "hydrated" flag would write an undecided
// record on a first visit, before the visitor chose anything.
export const CONSENT_COOKIE = "ee_consent"

export type ConsentState = {
  /** null until the visitor decides; then whether optional analytics may run. */
  analytics: boolean | null
  /** ISO timestamp of the decision. */
  decidedAt: string | null
  /** The banner's preferences view, reopened from "Cookie settings". */
  preferencesOpen: boolean
  acceptAll: () => void
  rejectOptional: () => void
  save: (choice: { analytics: boolean }) => void
  openPreferences: () => void
  closePreferences: () => void
}

export const useConsentStore = create<ConsentState>()(
  persist(
    (set) => ({
      analytics: null,
      decidedAt: null,
      preferencesOpen: false,
      acceptAll: () => set({ analytics: true, decidedAt: new Date().toISOString(), preferencesOpen: false }),
      rejectOptional: () => set({ analytics: false, decidedAt: new Date().toISOString(), preferencesOpen: false }),
      save: ({ analytics }) => set({ analytics, decidedAt: new Date().toISOString(), preferencesOpen: false }),
      openPreferences: () => set({ preferencesOpen: true }),
      closePreferences: () => set({ preferencesOpen: false }),
    }),
    {
      name: CONSENT_COOKIE,
      version: 1,
      storage: createJSONStorage(() => cookieStorage({ maxAgeDays: 365 })),
      partialize: (state) => ({ analytics: state.analytics, decidedAt: state.decidedAt }),
      skipHydration: true,
    },
  ),
)

/** True once the consent cookie has been read on this page view; false on the server. */
export function useConsentHydrated() {
  return useSyncExternalStore(
    (onChange) => useConsentStore.persist.onFinishHydration(onChange),
    () => useConsentStore.persist.hasHydrated(),
    () => false,
  )
}

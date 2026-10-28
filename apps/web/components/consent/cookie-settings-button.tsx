"use client"

import { useConsentStore } from "@/lib/stores/consent-store"

// Reopens the consent banner's preferences view (P0-076.1).
export function CookieSettingsButton() {
  const openPreferences = useConsentStore((state) => state.openPreferences)

  return (
    <button
      type="button"
      onClick={openPreferences}
      className="h-10 rounded-lg bg-foreground/5 px-4 text-[0.6875rem] font-bold uppercase tracking-[0.16em] text-foreground ring-1 ring-rule transition-colors hover:bg-foreground/10"
    >
      Change cookie settings
    </button>
  )
}

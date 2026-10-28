"use client"

import Link from "next/link"

import { useConsentStore } from "@/lib/stores/consent-store"

// Site footer (P0-076.1): the permanent way back into the cookie choice, and
// the policy link. Kept small; it's legal plumbing, not navigation.
export function SiteFooter() {
  const openPreferences = useConsentStore((state) => state.openPreferences)

  return (
    <footer className="mt-auto border-t border-rule">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-x-6 gap-y-2 px-5 py-6 text-[0.6875rem] font-bold uppercase tracking-[0.18em] text-muted-foreground md:px-8 lg:px-12">
        <span>© {new Date().getFullYear()} Skylights LLC</span>
        <Link href="/cookies" className="transition-colors hover:text-foreground">
          Cookie policy
        </Link>
        <button type="button" onClick={openPreferences} className="uppercase tracking-[0.18em] transition-colors hover:text-foreground">
          Cookie settings
        </button>
      </div>
    </footer>
  )
}

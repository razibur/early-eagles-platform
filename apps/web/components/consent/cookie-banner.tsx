"use client"

import Link from "next/link"
import { useId, useState } from "react"

import { FloatingCard, FloatingCardButton } from "@/components/shell/floating-card"
import { cn } from "@/lib/utils"
import { useConsentHydrated, useConsentStore } from "@/lib/stores/consent-store"

// Cookie consent (P0-076.1). Shown to a visitor who hasn't decided, and
// again whenever "Cookie settings" reopens it. Waits for the consent cookie
// to be read (useConsentHydrated) so it never flashes at someone who already chose.
//
// "Necessary only" carries the same weight as "Accept": refusing must be as
// easy as agreeing. Necessary storage (this decision, the theme) can't be
// switched off; analytics can, and stays off until accepted (P0-074 reads it).
export function CookieBanner() {
  const hydrated = useConsentHydrated()
  const { analytics, preferencesOpen, acceptAll, rejectOptional, save, openPreferences, closePreferences } =
    useConsentStore()
  const [draftAnalytics, setDraftAnalytics] = useState<boolean | null>(null)
  const titleId = useId()
  const switchId = useId()

  if (!hydrated || (analytics !== null && !preferencesOpen)) return null

  const analyticsOn = draftAnalytics ?? analytics ?? false
  const showPreferences = preferencesOpen

  return (
    <FloatingCard labelledBy={titleId}>
      <h2 id={titleId} className="display text-2xl text-foreground">
        {showPreferences ? "Cookie settings" : "Cookies, briefly"}
      </h2>
      <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
        We keep what the app needs to work. With your OK we&apos;d also count page visits to see what the club
        actually uses. No ads, nothing sold.{" "}
        <Link href="/cookies" className="text-foreground underline underline-offset-4 hover:text-primary">
          Cookie policy
        </Link>
      </p>

      {showPreferences && (
        <ul className="mt-5 divide-y divide-rule rounded-lg ring-1 ring-rule">
          <li className="flex items-start justify-between gap-4 p-3">
            <span>
              <span className="label block text-foreground">Necessary</span>
              <span className="mt-1 block text-xs text-muted-foreground">Your cookie choice, display theme and install card. Always on.</span>
            </span>
            <span className="label shrink-0 pt-0.5 text-primary">On</span>
          </li>
          <li className="flex items-start justify-between gap-4 p-3">
            <label htmlFor={switchId}>
              <span className="label block text-foreground">Analytics</span>
              <span className="mt-1 block text-xs text-muted-foreground">Anonymous page-visit counts. Off until you allow it.</span>
            </label>
            <button
              id={switchId}
              type="button"
              role="switch"
              aria-checked={analyticsOn}
              onClick={() => setDraftAnalytics(!analyticsOn)}
              className={cn(
                "relative mt-0.5 inline-flex h-6 w-11 shrink-0 items-center rounded-full ring-1 outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring",
                analyticsOn ? "bg-primary ring-primary" : "bg-muted ring-rule",
              )}
            >
              <span
                aria-hidden
                className={cn(
                  "inline-block size-4 rounded-full bg-foreground shadow transition-transform",
                  analyticsOn ? "translate-x-6" : "translate-x-1",
                )}
              />
            </button>
          </li>
        </ul>
      )}

      <div className="mt-5 grid grid-cols-2 gap-2 sm:flex sm:flex-wrap sm:items-center">
        {showPreferences ? (
          <>
            <FloatingCardButton
              tone="primary"
              onClick={() => {
                save({ analytics: analyticsOn })
                setDraftAnalytics(null)
              }}
            >
              Save choices
            </FloatingCardButton>
            <FloatingCardButton
              onClick={() => {
                setDraftAnalytics(null)
                if (analytics === null) rejectOptional()
                else closePreferences()
              }}
            >
              {analytics === null ? "Necessary only" : "Cancel"}
            </FloatingCardButton>
          </>
        ) : (
          <>
            <FloatingCardButton tone="primary" onClick={acceptAll}>
              Accept analytics
            </FloatingCardButton>
            <FloatingCardButton onClick={rejectOptional}>Necessary only</FloatingCardButton>
            <button
              type="button"
              onClick={openPreferences}
              className="col-span-2 h-10 text-[0.6875rem] font-bold uppercase tracking-[0.18em] text-muted-foreground underline-offset-4 transition-colors hover:text-foreground hover:underline sm:col-span-1 sm:ml-auto"
            >
              Preferences
            </button>
          </>
        )}
      </div>
    </FloatingCard>
  )
}

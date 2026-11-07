"use client"

import { Share } from "lucide-react"
import Image from "next/image"
import { useEffect, useId, useSyncExternalStore } from "react"

import { FloatingCard, FloatingCardButton } from "@/components/shell/floating-card"
import {
  acceptInstall,
  claimInstallCard,
  closeInstallCard,
  readInstallOffer,
  subscribeInstallOffer,
} from "@/lib/pwa/install-offer"
import { useConsentHydrated, useConsentStore } from "@/lib/stores/consent-store"

// "Add to home screen", asked once per device (P1-023). Waits for the cookie
// decision because the two cards share a spot and nobody should get both at once.
export function InstallCard() {
  // Browser-only state, so the server snapshot is null and the first render
  // matches the server HTML.
  const offer = useSyncExternalStore(subscribeInstallOffer, readInstallOffer, () => null)
  const hydrated = useConsentHydrated()
  const analytics = useConsentStore((state) => state.analytics)
  const preferencesOpen = useConsentStore((state) => state.preferencesOpen)
  const titleId = useId()

  const visible = offer !== null && hydrated && analytics !== null && !preferencesOpen

  useEffect(() => {
    if (visible) claimInstallCard()
  }, [visible])

  if (!visible) return null

  return (
    <FloatingCard labelledBy={titleId}>
      <div className="flex items-start gap-4">
        <Image
          src="/icons/icon-192.png"
          alt=""
          width={52}
          height={52}
          unoptimized
          className="size-13 shrink-0 rounded-xl ring-1 ring-rule"
        />
        <div className="min-w-0">
          <h2 id={titleId} className="display text-2xl text-foreground">
            Keep the club a tap away
          </h2>
          <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
            Add Early Eagles to your home screen. It opens full screen, like any other app.
          </p>
        </div>
      </div>

      {offer === "ios" ? (
        <>
          <ol className="mt-5 divide-y divide-rule rounded-lg ring-1 ring-rule">
            <Step number={1}>
              Tap <strong className="font-semibold text-foreground">Share</strong>{" "}
              <Share aria-hidden className="inline size-4 -translate-y-px text-primary" />. In Safari it may be under
              the ⋯ button.
            </Step>
            <Step number={2}>
              Choose <strong className="font-semibold text-foreground">Add to Home Screen</strong>.
            </Step>
          </ol>
          <div className="mt-5 grid">
            <FloatingCardButton tone="primary" onClick={closeInstallCard}>
              Got it
            </FloatingCardButton>
          </div>
        </>
      ) : (
        <div className="mt-5 grid grid-cols-2 gap-2 sm:flex">
          <FloatingCardButton tone="primary" onClick={acceptInstall}>
            Install
          </FloatingCardButton>
          <FloatingCardButton onClick={closeInstallCard}>Not now</FloatingCardButton>
        </div>
      )}
    </FloatingCard>
  )
}

function Step({ number, children }: { number: number; children: React.ReactNode }) {
  return (
    <li className="flex items-center gap-3 p-3">
      <span aria-hidden className="display w-5 shrink-0 text-center text-2xl text-primary">
        {number}
      </span>
      <span className="text-sm leading-relaxed text-muted-foreground">{children}</span>
    </li>
  )
}

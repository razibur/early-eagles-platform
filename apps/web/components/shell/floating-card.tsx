"use client"

import { useEffect, useRef } from "react"

import { cn } from "@/lib/utils"

// The card that floats above the bottom nav: the cookie banner (P0-076.1) and
// the install card (P1-023). They share one spot, so only one shows at a time.
// Non-modal on purpose: the page behind stays usable and focus isn't taken.
//
// While it shows, it publishes its height (plus its gap) as
// --floating-card-space on <html>, and the body's bottom padding adds it
// (app/layout.tsx). Otherwise, on a short screen such as an iPhone with
// Safari's toolbars, the end of the page can't scroll out from under the card
// and buttons there can't be tapped until the card is dismissed (P0-070.6).
const SPACE = "--floating-card-space"
const GAP_PX = 24

export function FloatingCard({ labelledBy, children }: { labelledBy: string; children: React.ReactNode }) {
  const ref = useRef<HTMLElement>(null)

  useEffect(() => {
    const card = ref.current
    if (!card) return
    const root = document.documentElement
    const publish = () => root.style.setProperty(SPACE, `${Math.ceil(card.getBoundingClientRect().height) + GAP_PX}px`)
    publish()
    const observer = new ResizeObserver(publish)
    observer.observe(card)
    return () => {
      observer.disconnect()
      root.style.removeProperty(SPACE)
    }
  }, [])

  return (
    <section
      ref={ref}
      role="dialog"
      aria-modal="false"
      aria-labelledby={labelledBy}
      className="fixed inset-x-3 bottom-[calc(4rem+1px+env(safe-area-inset-bottom)+0.75rem)] z-50 mx-auto max-w-xl animate-in fade-in slide-in-from-bottom-4 duration-500 md:bottom-6"
    >
      <div className="relative overflow-hidden rounded-xl bg-popover/95 p-5 shadow-2xl ring-1 ring-rule backdrop-blur-md md:p-6">
        <span aria-hidden className="absolute inset-x-0 top-0 flex h-1">
          <span className="flex-2 bg-primary" />
          <span className="flex-1 bg-destructive" />
          <span className="flex-6 bg-rule" />
        </span>
        {children}
      </div>
    </section>
  )
}

export function FloatingCardButton({
  tone = "neutral",
  onClick,
  children,
}: {
  tone?: "primary" | "neutral"
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "h-10 rounded-lg px-4 text-[0.6875rem] font-bold uppercase tracking-[0.16em] outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring",
        tone === "primary"
          ? "bg-primary text-primary-foreground hover:bg-primary/85"
          : "bg-foreground/5 text-foreground ring-1 ring-rule hover:bg-foreground/10",
      )}
    >
      {children}
    </button>
  )
}

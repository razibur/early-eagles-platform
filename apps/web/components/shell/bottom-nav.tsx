"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"
import { isActive, navItems } from "@/lib/navigation"

// Phone tab bar (P0-070.2): fixed to the bottom below the md breakpoint
// (768px), hidden above it where the top bar carries the links. The active
// section gets the brand green and a court-line marker along its top edge.
// Pads for the home indicator (safe-area-inset-bottom) so the bar clears it
// when the app runs installed.
export function BottomNav() {
  const pathname = usePathname()

  return (
    <nav
      aria-label="Sections"
      className="fixed inset-x-0 bottom-0 z-40 border-t border-rule bg-background/90 pb-[env(safe-area-inset-bottom)] backdrop-blur-md md:hidden"
    >
      <ul className="mx-auto grid max-w-lg grid-cols-6">
        {navItems.map(({ href, label, icon: Icon }) => {
          const active = isActive(href, pathname)
          return (
            <li key={href}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "relative flex h-16 flex-col items-center justify-center gap-1 text-[0.625rem] font-bold uppercase tracking-[0.12em] outline-none transition-colors focus-visible:bg-foreground/5",
                  active ? "text-primary" : "text-muted-foreground hover:text-foreground",
                )}
              >
                <span
                  aria-hidden
                  className={cn(
                    "absolute inset-x-3 top-0 h-0.5 origin-center bg-primary transition-transform duration-300",
                    active ? "scale-x-100" : "scale-x-0",
                  )}
                />
                <Icon className="size-5" strokeWidth={active ? 2.25 : 1.75} aria-hidden />
                {label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

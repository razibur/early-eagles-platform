"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"
import { isActive, navItems } from "@/lib/navigation"

// Section links in the top bar from md (768px) up; below that the bottom bar
// takes over (P0-070.2). Settings lives in the account menu here, so it is
// left out of this row.
export function DesktopNav() {
  const pathname = usePathname()

  return (
    <nav aria-label="Sections" className="hidden md:block">
      <ul className="flex items-center gap-1">
        {navItems
          .filter((item) => item.href !== "/settings")
          .map(({ href, label }) => {
            const active = isActive(href, pathname)
            return (
              <li key={href}>
                <Link
                  href={href}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "relative inline-flex h-14 items-center px-3 text-[0.6875rem] font-bold uppercase tracking-[0.18em] outline-none transition-colors focus-visible:text-foreground",
                    active ? "text-foreground" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {label}
                  <span
                    aria-hidden
                    className={cn(
                      "absolute inset-x-3 bottom-0 h-0.5 bg-primary transition-transform duration-300",
                      active ? "scale-x-100" : "scale-x-0",
                    )}
                  />
                </Link>
              </li>
            )
          })}
      </ul>
    </nav>
  )
}

import {
  CalendarDays,
  House,
  Images,
  MessagesSquare,
  Settings,
  UsersRound,
  type LucideIcon,
} from "lucide-react"

// The app's top-level sections (P0-070.2). One list drives both the phone
// bottom bar and the desktop links in the top bar, so they can't drift.
// `phase` marks sections whose page is still a placeholder; the
// ComingSoon page reads it.
export type NavItem = {
  href: string
  label: string
  icon: LucideIcon
  phase?: { number: number; summary: string }
}

export const navItems: NavItem[] = [
  { href: "/", label: "Home", icon: House },
  {
    href: "/wall",
    label: "Wall",
    icon: Images,
    phase: { number: 4, summary: "Club photo wall: Sunday snaps, reactions and short comments." },
  },
  {
    href: "/roster",
    label: "Roster",
    icon: UsersRound,
    phase: { number: 1, summary: "Member directory with roles, attendance streaks and the leaderboard." },
  },
  {
    href: "/events",
    label: "Events",
    icon: CalendarDays,
    phase: { number: 4, summary: "Picnics, fishing trips and tournaments, with guest and family RSVPs." },
  },
  {
    href: "/chat",
    label: "Chat",
    icon: MessagesSquare,
    phase: { number: 3, summary: "Club channel and direct messages, in real time." },
  },
  {
    href: "/settings",
    label: "Settings",
    icon: Settings,
    phase: { number: 1, summary: "Your profile, sign-in methods and notifications." },
  },
]

export function isActive(href: string, pathname: string) {
  return href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`)
}

export function navItem(href: string): NavItem {
  const item = navItems.find((i) => i.href === href)
  if (!item) throw new Error(`No nav item for ${href}`)
  return item
}

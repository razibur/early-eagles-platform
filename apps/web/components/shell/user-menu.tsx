"use client"

import Link from "next/link"
import { Menu as MenuPrimitive } from "@base-ui/react/menu"
import { LogIn, Settings, UserRound } from "lucide-react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

// The account menu in the top bar (P0-070.1). There is no signed-in user
// until Clerk lands (P0-039), so it says so plainly instead of faking one:
// a guest avatar, a disabled sign-in entry, and the links that don't need
// an account.
export function UserMenu() {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Account menu"
        className="group inline-flex size-9 items-center justify-center rounded-full bg-foreground/5 ring-1 ring-rule outline-none transition-colors hover:bg-foreground/10 hover:ring-primary/60 focus-visible:ring-2 focus-visible:ring-ring data-[popup-open]:ring-primary"
      >
        <UserRound className="size-4 text-muted-foreground transition-colors group-hover:text-foreground" aria-hidden />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" sideOffset={8} className="min-w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <span className="label block">Guest</span>
            <span className="mt-1 block text-xs font-normal normal-case tracking-normal text-muted-foreground">
              Not signed in
            </span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled>
          <LogIn aria-hidden />
          <span className="flex flex-col">
            <span>Sign in</span>
            <span className="text-[0.6875rem] text-muted-foreground">Arrives with member accounts</span>
          </span>
        </DropdownMenuItem>
        <MenuPrimitive.LinkItem
          render={<Link href="/settings" />}
          className="relative flex cursor-default items-center gap-2 rounded-md px-1.5 py-1 text-sm outline-hidden select-none data-highlighted:bg-accent data-highlighted:text-accent-foreground [&_svg]:size-4 [&_svg]:shrink-0"
        >
          <Settings aria-hidden />
          Settings
        </MenuPrimitive.LinkItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

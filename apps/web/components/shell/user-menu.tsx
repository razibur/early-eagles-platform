"use client"

import Link from "next/link"
import { Menu as MenuPrimitive } from "@base-ui/react/menu"
import { IdCard, LogIn, LogOut, Settings, UserRound } from "lucide-react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

/** The signed-in person, as the menu shows them. */
export type MenuAccount = {
  name: string
  email: string | null
  imageUrl: string | null
}

const linkItemClass =
  "relative flex cursor-default items-center gap-2 rounded-md px-1.5 py-1 text-sm outline-hidden select-none data-highlighted:bg-accent data-highlighted:text-accent-foreground [&_svg]:size-4 [&_svg]:shrink-0"

// The account menu in the top bar (P0-070.1, signed-in state P0-039.5).
// Presentational only: ClerkUserMenu fills `account` and `onSignOut` from
// Clerk; a build without Clerk renders it bare. No account means a guest
// avatar and a disabled sign-in entry, never a faked user.
export function UserMenu({
  account,
  onSignOut,
  signInHref,
}: {
  account?: MenuAccount | null
  onSignOut?: () => void
  /** Where "Sign in" goes; without it sign-in is shown as unavailable. */
  signInHref?: string
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={account ? `Account menu for ${account.name}` : "Account menu"}
        className="group inline-flex size-9 items-center justify-center overflow-hidden rounded-full bg-foreground/5 ring-1 ring-rule outline-none transition-colors hover:bg-foreground/10 hover:ring-primary/60 focus-visible:ring-2 focus-visible:ring-ring data-[popup-open]:ring-primary"
      >
        <Avatar account={account} />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" sideOffset={8} className="min-w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <span className="label block">{account ? account.name : "Guest"}</span>
            <span className="mt-1 block truncate text-xs font-normal normal-case tracking-normal text-muted-foreground">
              {account ? (account.email ?? "Signed in") : "Not signed in"}
            </span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {!account &&
          (signInHref ? (
            <MenuPrimitive.LinkItem render={<Link href={signInHref} />} className={linkItemClass}>
              <LogIn aria-hidden />
              Sign in
            </MenuPrimitive.LinkItem>
          ) : (
            <DropdownMenuItem disabled>
              <LogIn aria-hidden />
              <span className="flex flex-col">
                <span>Sign in</span>
                <span className="text-[0.6875rem] text-muted-foreground">Not available in this build</span>
              </span>
            </DropdownMenuItem>
          ))}
        {account && (
          <MenuPrimitive.LinkItem render={<Link href="/account" />} className={linkItemClass}>
            <IdCard aria-hidden />
            Account
          </MenuPrimitive.LinkItem>
        )}
        <MenuPrimitive.LinkItem render={<Link href="/settings" />} className={linkItemClass}>
          <Settings aria-hidden />
          Settings
        </MenuPrimitive.LinkItem>
        {account && onSignOut && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={onSignOut}>
              <LogOut aria-hidden />
              Sign out
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function Avatar({ account }: { account?: MenuAccount | null }) {
  if (account?.imageUrl) {
    // eslint-disable-next-line @next/next/no-img-element -- Clerk serves sized avatars from its own CDN; next/image would need a remote pattern for no gain at 36 px
    return <img src={account.imageUrl} alt="" width={36} height={36} referrerPolicy="no-referrer" className="size-full object-cover" />
  }
  if (account) {
    return (
      <span aria-hidden className="font-display text-sm font-extrabold uppercase text-foreground">
        {initials(account.name)}
      </span>
    )
  }
  return <UserRound className="size-4 text-muted-foreground transition-colors group-hover:text-foreground" aria-hidden />
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const letters = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : (parts[0] ?? "?").slice(0, 2)
  return letters.toUpperCase()
}

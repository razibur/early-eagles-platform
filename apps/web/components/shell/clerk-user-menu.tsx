"use client"

import { useClerk, useUser } from "@clerk/nextjs"

import { UserMenu, type MenuAccount } from "@/components/shell/user-menu"
import { SIGN_IN_PATH } from "@/lib/auth/routes"

// UserMenu filled from Clerk (P0-039.5). Rendered only under ClerkProvider,
// i.e. when authMode() is "clerk"; the top bar picks this or the bare menu.
// While Clerk loads, the guest avatar shows (no flash of a fake name).
export function ClerkUserMenu() {
  const { isLoaded, isSignedIn, user } = useUser()
  const { signOut } = useClerk()

  const account: MenuAccount | null =
    isLoaded && isSignedIn
      ? {
          name: user.fullName?.trim() || user.username || user.primaryEmailAddress?.emailAddress || "Member",
          email: user.primaryEmailAddress?.emailAddress ?? null,
          // Clerk always returns an imageUrl (a generated one when the user
          // has none); only a real photo beats our initials.
          imageUrl: user.hasImage ? user.imageUrl : null,
        }
      : null

  return <UserMenu account={account} signInHref={SIGN_IN_PATH} onSignOut={() => void signOut({ redirectUrl: SIGN_IN_PATH })} />
}

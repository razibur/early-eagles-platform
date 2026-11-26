import type { Metadata } from "next"

import { AccountPanel } from "@/components/account/account-panel"

export const metadata: Metadata = { title: "Your account — Early Eagles" }

// Your account as the club sees it (P0-071.3). Signed-out visitors never get
// here with Clerk on (proxy.ts sends them to /sign-in); in a build without
// Clerk the panel shows what the API says to an anonymous caller.
export default function AccountPage() {
  return (
    <main className="mx-auto w-full max-w-3xl flex-1 px-5 py-12 md:px-8 md:py-16">
      <span className="label label-accent text-primary">Account</span>
      <h1 className="display mt-6 text-5xl text-foreground md:text-7xl">You, at the club</h1>
      <p className="mt-6 max-w-prose text-base leading-relaxed text-muted-foreground">
        What the club&apos;s server knows about you at this address: which club it is, and your place in it.
      </p>
      <AccountPanel />
    </main>
  )
}

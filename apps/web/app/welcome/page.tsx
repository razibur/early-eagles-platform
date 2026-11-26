import type { Metadata } from "next"

import { RoleLanding } from "@/components/auth/role-landing"

export const metadata: Metadata = { title: "Welcome — Early Eagles" }

// Where sign-in and sign-up land (AFTER_AUTH_PATH, P1-035). It only decides
// where to go next: admins to /admin, everyone else to /.
export default function WelcomePage() {
  return (
    <main className="mx-auto grid w-full max-w-3xl flex-1 place-items-center px-5 py-24">
      <RoleLanding />
    </main>
  )
}

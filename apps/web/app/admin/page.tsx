import type { Metadata } from "next"

import { AdminGate } from "@/components/admin/admin-gate"
import { PaymentsPanel } from "@/components/admin/payments-panel"

export const metadata: Metadata = { title: "Club desk — Early Eagles" }

// The admin home (ADR-0004). P1-017 brings the dues desk; P1-033 adds the
// RSVP grid and the announcement composer; P1-035 sends admins here after
// sign-in.
export default function AdminPage() {
  return (
    <main className="mx-auto w-full max-w-5xl flex-1 px-5 py-12 md:px-8 md:py-16">
      <span className="label label-accent text-primary">Admin</span>
      <h1 className="display mt-6 text-5xl text-foreground md:text-7xl">Club desk</h1>
      <p className="mt-6 max-w-prose text-base leading-relaxed text-muted-foreground">
        Settle each Sunday: check reported Zelle payments, confirm them, or send one back with a reason.
      </p>
      <AdminGate>
        <PaymentsPanel />
      </AdminGate>
    </main>
  )
}

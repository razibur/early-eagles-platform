import type { Metadata } from "next"

import { AdminGate } from "@/components/admin/admin-gate"
import { PaymentsPanel } from "@/components/admin/payments-panel"
import { RollCallPanel } from "@/components/admin/roll-call-panel"

export const metadata: Metadata = { title: "Club desk — Early Eagles" }

// The admin home (ADR-0004): the roll call for the next session (P1-033.1),
// then the dues desk (P1-017). P1-035 sends admins here after sign-in; the
// announcement composer joins once announcements exist (P1-020).
export default function AdminPage() {
  return (
    <main className="mx-auto w-full max-w-5xl flex-1 px-5 py-12 md:px-8 md:py-16">
      <span className="label label-accent text-primary">Admin</span>
      <h1 className="display mt-6 text-5xl text-foreground md:text-7xl">Club desk</h1>
      <p className="mt-6 max-w-prose text-base leading-relaxed text-muted-foreground">
        See who&apos;s coming this Sunday and who hasn&apos;t answered, then settle dues: check reported Zelle payments, confirm them, or send one back with a reason.
      </p>
      <AdminGate>
        <RollCallPanel />
        <PaymentsPanel />
      </AdminGate>
    </main>
  )
}

import type { Metadata } from "next"

import { ComingSoon } from "@/components/shell/coming-soon"

export const metadata: Metadata = { title: "Roster — Early Eagles" }

export default function RosterPage() {
  return <ComingSoon href="/roster" />
}

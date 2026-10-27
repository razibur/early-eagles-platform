import type { Metadata } from "next"

import { ComingSoon } from "@/components/shell/coming-soon"

export const metadata: Metadata = { title: "Events — Early Eagles" }

export default function EventsPage() {
  return <ComingSoon href="/events" />
}

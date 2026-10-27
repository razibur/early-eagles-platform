import type { Metadata } from "next"

import { ComingSoon } from "@/components/shell/coming-soon"

export const metadata: Metadata = { title: "Settings — Early Eagles" }

export default function SettingsPage() {
  return <ComingSoon href="/settings" />
}

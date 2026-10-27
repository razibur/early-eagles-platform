import type { Metadata } from "next"

import { ComingSoon } from "@/components/shell/coming-soon"

export const metadata: Metadata = { title: "Chat — Early Eagles" }

export default function ChatPage() {
  return <ComingSoon href="/chat" />
}

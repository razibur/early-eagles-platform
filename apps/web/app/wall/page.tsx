import type { Metadata } from "next"

import { ComingSoon } from "@/components/shell/coming-soon"

export const metadata: Metadata = { title: "Wall — Early Eagles" }

export default function WallPage() {
  return <ComingSoon href="/wall" />
}

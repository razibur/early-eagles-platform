import type { Metadata } from "next"

import { StandingsTable } from "@/components/roster/standings-table"

export const metadata: Metadata = { title: "Roster — Early Eagles" }

// The club's members as the year's standings (P1-018, P1-019): who has played
// most, who has paid, who was there last. A directory with roles and contact
// details can follow once the API lists members on their own.
export default function RosterPage() {
  return (
    <main className="mx-auto w-full max-w-4xl flex-1 px-5 py-12 md:px-8 md:py-16">
      <span className="label label-accent text-primary">Roster · Year to date</span>
      <h1 className="display mt-6 text-5xl text-foreground md:text-7xl">The standings</h1>
      <p className="mt-6 max-w-prose text-base leading-relaxed text-muted-foreground">
        Everyone in the club this year, most Sundays played first. Tap a column to sort by it.
      </p>
      <StandingsTable />
    </main>
  )
}

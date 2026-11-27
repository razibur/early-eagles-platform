"use client"

import { useQuery } from "@tanstack/react-query"
import { ArrowDown, ArrowUp } from "lucide-react"
import { useState } from "react"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError } from "@/lib/api/client"
import {
  firstDirection,
  formatPlayed,
  isStandingList,
  rank,
  sortStandings,
  type RankedRow,
  type SortDirection,
  type StandingKey,
} from "@/lib/roster/standings"
import { cn } from "@/lib/utils"

// The roster as the year's standings (P1-019, P1-018): every current member
// with sessions played, times paid and last played, from
// GET /leaderboard?period=year. Any column sorts; your own row is marked.

type Me = { userId: string }

// `spoken` names the column for screen readers where the label is terse.
const columns: { key: StandingKey; label: string; spoken: string; numeric: boolean; className?: string }[] = [
  { key: "standing", label: "#", spoken: "standing", numeric: true, className: "w-12" },
  { key: "name", label: "Member", spoken: "name", numeric: false },
  { key: "sessionsAttended", label: "Played", spoken: "sessions played", numeric: true },
  { key: "timesPaid", label: "Paid", spoken: "times paid", numeric: true },
  { key: "lastPlayed", label: "Last", spoken: "last played", numeric: true, className: "hidden sm:table-cell" },
]

export function StandingsTable() {
  const apiFetch = useApiFetch()
  const board = useQuery({
    queryKey: ["leaderboard", "year"],
    queryFn: async () => {
      const rows = await apiFetch<unknown>("/leaderboard?period=year")
      if (!isStandingList(rows)) throw new Error("Unexpected leaderboard")
      return rank(rows)
    },
    retry: false,
  })
  const me = useQuery({ queryKey: ["me"], queryFn: () => apiFetch<Me>("/me"), retry: false })
  const [sort, setSort] = useState<{ key: StandingKey; direction: SortDirection }>({ key: "standing", direction: "ascending" })

  if (board.isPending) return <span aria-label="Loading the roster" className="mt-10 block h-72 w-full animate-pulse rounded-lg bg-muted motion-reduce:animate-none" />
  if (board.isError) return <Notice>{problemText(board.error)}</Notice>
  if (board.data.length === 0) return <Notice>No members on the roster yet.</Notice>

  const rows = sortStandings(board.data, sort.key, sort.direction)
  const choose = (key: StandingKey) =>
    setSort((current) =>
      current.key === key
        ? { key, direction: current.direction === "ascending" ? "descending" : "ascending" }
        : { key, direction: firstDirection(key) },
    )

  return (
    <div className="mt-10 overflow-hidden rounded-lg bg-card ring-1 ring-rule">
      <table className="w-full border-collapse text-left">
        <caption className="sr-only">Club roster, sorted by {columns.find((c) => c.key === sort.key)!.spoken}</caption>
        <thead>
          <tr className="border-b border-rule">
            {columns.map((column) => {
              const active = sort.key === column.key
              const Arrow = sort.direction === "ascending" ? ArrowUp : ArrowDown
              return (
                <th
                  key={column.key}
                  scope="col"
                  aria-sort={active ? sort.direction : "none"}
                  className={cn("p-0", column.className)}
                >
                  <button
                    type="button"
                    onClick={() => choose(column.key)}
                    aria-label={column.key === "standing" ? "Standing" : column.key === "lastPlayed" ? "Last played" : undefined}
                    className={cn(
                      "label flex min-h-11 w-full items-center gap-1 px-3 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring md:px-4",
                      column.numeric && column.key !== "standing" && "justify-end",
                      active ? "text-foreground" : "text-muted-foreground hover:text-foreground",
                    )}
                  >
                    {column.label}
                    <Arrow aria-hidden className={cn("size-3", !active && "invisible")} />
                  </button>
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <Row key={row.clerkUserId} row={row} isMe={me.data?.userId === row.clerkUserId} />
          ))}
        </tbody>
      </table>
      <p className="border-t border-rule px-3 py-3 text-xs text-muted-foreground md:px-4">
        Played counts every Sunday you said yes to that went ahead. Paid counts confirmed dues.
      </p>
    </div>
  )
}

function Row({ row, isMe }: { row: RankedRow; isMe: boolean }) {
  return (
    <tr aria-current={isMe ? "true" : undefined} className={cn("border-b border-rule last:border-b-0", isMe && "bg-primary/10")}>
      <td className="display figures px-3 py-3 text-2xl text-muted-foreground md:px-4">{row.rank}</td>
      <th scope="row" className="px-3 py-3 font-semibold md:px-4">
        <span className={cn(row.displayName === null ? "italic text-muted-foreground" : "text-foreground")}>{row.displayName ?? "Unnamed member"}</span>
        {isMe && <span className="label ml-2 text-primary">You</span>}
      </th>
      <td className="display figures px-3 py-3 text-right text-2xl text-foreground md:px-4">{row.sessionsAttended}</td>
      <td className="display figures px-3 py-3 text-right text-2xl text-foreground md:px-4">{row.timesPaid}</td>
      <td className="figures hidden px-3 py-3 text-right text-sm text-muted-foreground sm:table-cell md:px-4">
        {row.lastPlayed ? formatPlayed(row.lastPlayed) : "—"}
      </td>
    </tr>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return <p className="mt-10 rounded-lg bg-card p-5 text-muted-foreground ring-1 ring-rule md:p-8">{children}</p>
}

function problemText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Sign in to see the roster."
    if (error.status === 403) return "You're not on this club's roster yet. Your club's admin adds you."
    if (error.status === 400 || error.status === 404) return "The roster isn't open at this address yet."
  }
  return "Couldn't reach the club right now."
}

"use client"

import { useQuery } from "@tanstack/react-query"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError } from "@/lib/api/client"
import { fetchNextSession, NEXT_SESSION_KEY } from "@/lib/sessions/fetch-next"
import { describeSession, type RsvpAnswer, type SessionSummary } from "@/lib/sessions/next-session"
import { cn } from "@/lib/utils"

// The admin's roll call for the next session (P1-033.1): every member against
// it, so the admin sees who's in and who to chase before Sunday. Reads
// GET /sessions/{id}/rsvps (P1-007.2), admin only, which lists every current
// member by name (unnamed last) with their answer, or null for no answer yet.

export type RsvpGridRow = {
  clerkUserId: string
  displayName: string | null
  imageUrl: string | null
  response: RsvpAnswer | null
}

const rsvpsKey = (sessionId: string) => ["sessions", sessionId, "rsvps"] as const

/** The roll call's groups, in the order the admin reads them: who to chase first. */
export function groupRollCall(rows: RsvpGridRow[]) {
  return {
    unanswered: rows.filter((r) => r.response === null),
    yes: rows.filter((r) => r.response === "Yes"),
    maybe: rows.filter((r) => r.response === "Maybe"),
    no: rows.filter((r) => r.response === "No"),
  }
}

export function RollCallPanel() {
  const apiFetch = useApiFetch()
  const next = useQuery({ queryKey: NEXT_SESSION_KEY, queryFn: () => fetchNextSession(apiFetch), retry: false })

  return (
    <section aria-labelledby="roll-call" className="mt-10">
      <h2 id="roll-call" className="display text-3xl uppercase text-foreground md:text-4xl">
        Roll call
      </h2>
      {next.isPending ? (
        <Skeleton />
      ) : next.isError ? (
        <Notice>{problemText(next.error)}</Notice>
      ) : next.data === null ? (
        <Notice>No session on the board this week or next.</Notice>
      ) : next.data.status === "Cancelled" ? (
        <Notice>{line(next.data)}: called off. Nobody to chase.</Notice>
      ) : (
        <Grid key={next.data.id} session={next.data} />
      )}
    </section>
  )
}

function Grid({ session }: { session: SessionSummary }) {
  const apiFetch = useApiFetch()
  const rows = useQuery({
    queryKey: rsvpsKey(session.id),
    queryFn: async () => {
      const grid = await apiFetch<unknown>(`/sessions/${session.id}/rsvps`)
      if (!Array.isArray(grid)) throw new Error("Unexpected roll call")
      return grid as RsvpGridRow[]
    },
    retry: false,
  })

  if (rows.isPending) return <Skeleton />
  if (rows.isError) return <Notice>{problemText(rows.error)}</Notice>

  const groups = groupRollCall(rows.data)

  return (
    <div className="mt-6 overflow-hidden rounded-lg bg-card ring-1 ring-rule">
      <dl className="grid grid-cols-2 gap-px bg-rule sm:grid-cols-4">
        <Figure label="In" value={groups.yes.length} tone="text-primary" />
        <Figure label="Maybe" value={groups.maybe.length} />
        <Figure label="Out" value={groups.no.length} />
        <Figure label="No answer" value={groups.unanswered.length} tone={groups.unanswered.length ? "text-shuttle" : "text-foreground"} />
      </dl>
      <p className="border-t border-rule px-5 py-3 text-sm text-muted-foreground md:px-6">{line(session)}</p>

      {rows.data.length === 0 ? (
        <p className="border-t border-rule px-5 py-8 text-center font-semibold text-foreground md:px-6">No members on the roster yet.</p>
      ) : (
        <>
          <Group title="No answer yet" hint="Worth a nudge before Sunday." rows={groups.unanswered} emphasis />
          <Group title="In" hint="Said yes." rows={groups.yes} />
          <Group title="Maybe" hint="Not sure yet." rows={groups.maybe} />
          <Group title="Out" hint="Can't make it." rows={groups.no} />
        </>
      )}
    </div>
  )
}

function Group({ title, hint, rows, emphasis = false }: { title: string; hint: string; rows: RsvpGridRow[]; emphasis?: boolean }) {
  if (rows.length === 0) return null
  return (
    <section aria-label={title} className="border-t border-rule px-5 py-4 md:px-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="label flex items-center gap-2 text-foreground">
          {emphasis && <span aria-hidden className="h-3 w-1 rounded-full bg-shuttle" />}
          {title} <span className="figures text-muted-foreground">· {rows.length}</span>
        </h3>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </div>
      <ul className="mt-3 flex flex-wrap gap-2">
        {rows.map((r) => (
          <li
            key={r.clerkUserId}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm ring-1",
              emphasis ? "bg-shuttle/10 font-semibold text-foreground ring-shuttle/40" : "bg-background text-foreground ring-rule",
              r.displayName === null && "italic text-muted-foreground",
            )}
          >
            {r.displayName ?? "Unnamed member"}
          </li>
        ))}
      </ul>
    </section>
  )
}

function line(session: SessionSummary): string {
  const when = describeSession(session)
  return `${when.weekday}, ${when.month} ${when.day} · ${when.time} · ${session.venue}`
}

function Figure({ label, value, tone = "text-foreground" }: { label: string; value: number; tone?: string }) {
  return (
    <div className="flex flex-col gap-1 bg-card px-5 py-4 md:px-6">
      <dt className="label text-muted-foreground">{label}</dt>
      <dd className={cn("display figures text-4xl", tone)}>{value}</dd>
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return <p className="mt-6 rounded-lg bg-card p-5 text-muted-foreground ring-1 ring-rule md:p-8">{children}</p>
}

function Skeleton() {
  return <span aria-label="Loading the roll call" className="mt-6 block h-48 w-full animate-pulse rounded-lg bg-muted motion-reduce:animate-none" />
}

function problemText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Sign in as a club admin to use this page."
    if (error.status === 403) return "This page is for the club's admins."
    if (error.status === 400 || error.status === 404) return "Sessions aren't open at this address yet."
  }
  return "Couldn't reach the club right now."
}

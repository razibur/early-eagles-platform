"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useId, useState } from "react"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError, type ApiFetch } from "@/lib/api/client"
import {
  describeSession,
  formatDollars,
  isSessionList,
  isoWeekAfter,
  isoWeekBefore,
  isoWeekOf,
  sessionToSettle,
  startOf,
  type SessionSummary,
} from "@/lib/sessions/next-session"
import { cn } from "@/lib/utils"

// The admin's dues desk for one session (P1-017): reported payments to
// check against the Zelle app, one tap to confirm, a reason to send one back.
// Reads GET /sessions/{id}/payments (P1-009.3), which already leaves out
// confirmed payments and anyone who didn't say Yes; acts with
// POST …/payments/{memberId}/confirm and …/reject (P1-009, P1-009.2).

export type UnsettledRow = {
  clerkUserId: string
  displayName: string | null
  amountCents: number
  status: "Reported" | "Rejected" | null
  senderName: string | null
  reference: string | null
  rejectionReason: string | null
}

/** The API's limit for a rejection reason (SessionPayment.RejectionReasonMaxLength). */
export const REJECTION_REASON_MAX = 500

const paymentsKey = (sessionId: string) => ["sessions", sessionId, "payments"] as const

async function fetchRecentSessions(apiFetch: ApiFetch): Promise<SessionSummary[]> {
  const now = new Date()
  const weeks = [isoWeekBefore(now), isoWeekOf(now), isoWeekAfter(now)]
  const lists = await Promise.all(weeks.map((w) => apiFetch<unknown>(`/sessions?week=${w}`)))
  const all: SessionSummary[] = []
  for (const list of lists) {
    if (!isSessionList(list)) throw new Error("Unexpected session list")
    all.push(...list)
  }
  return all.filter((s) => s.status !== "Cancelled" && s.feeCents > 0).sort((a, b) => startOf(a).getTime() - startOf(b).getTime())
}

export function PaymentsPanel() {
  const apiFetch = useApiFetch()
  const sessions = useQuery({ queryKey: ["sessions", "recent"], queryFn: () => fetchRecentSessions(apiFetch), retry: false })
  const [picked, setPicked] = useState<string | null>(null)

  if (sessions.isPending) return <Skeleton />
  if (sessions.isError) return <Notice>{problemText(sessions.error)}</Notice>
  if (sessions.data.length === 0) return <Notice>No paid sessions last week, this week or next.</Notice>

  const chosen = sessions.data.find((s) => s.id === picked) ?? sessionToSettle(sessions.data, new Date())!

  return (
    <section aria-labelledby="dues-desk" className="mt-10">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h2 id="dues-desk" className="display text-3xl uppercase text-foreground md:text-4xl">
          Dues desk
        </h2>
        <div role="group" aria-label="Session" className="flex flex-wrap gap-2">
          {sessions.data.map((s) => {
            const when = describeSession(s)
            const active = s.id === chosen.id
            return (
              <button
                key={s.id}
                type="button"
                aria-pressed={active}
                onClick={() => setPicked(s.id)}
                className={cn(
                  "min-h-11 rounded-md px-3 text-sm font-semibold ring-1 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                  active ? "bg-foreground text-background ring-foreground" : "bg-card text-muted-foreground ring-rule hover:text-foreground",
                )}
              >
                <span className="figures">
                  {when.weekday.slice(0, 3)} {when.month} {when.day}
                </span>
              </button>
            )
          })}
        </div>
      </div>
      <SessionLedger key={chosen.id} session={chosen} />
    </section>
  )
}

function SessionLedger({ session }: { session: SessionSummary }) {
  const apiFetch = useApiFetch()
  const rows = useQuery({
    queryKey: paymentsKey(session.id),
    queryFn: () => apiFetch<UnsettledRow[]>(`/sessions/${session.id}/payments`),
    retry: false,
  })
  const when = describeSession(session)

  if (rows.isPending) return <Skeleton />
  if (rows.isError) return <Notice>{problemText(rows.error)}</Notice>

  const reported = rows.data.filter((r) => r.status === "Reported")
  const rejected = rows.data.filter((r) => r.status === "Rejected")
  const silent = rows.data.filter((r) => r.status === null)
  const outstanding = rows.data.reduce((sum, r) => sum + r.amountCents, 0)

  return (
    <div className="mt-6 overflow-hidden rounded-lg bg-card ring-1 ring-rule">
      <dl className="grid grid-cols-2 gap-px bg-rule sm:grid-cols-4">
        <Figure label="To check" value={reported.length} tone={reported.length ? "text-shuttle" : "text-foreground"} />
        <Figure label="Sent back" value={rejected.length} />
        <Figure label="Not reported" value={silent.length} />
        <Figure label="Outstanding" value={formatDollars(outstanding)} />
      </dl>
      <p className="border-t border-rule px-5 py-3 text-sm text-muted-foreground md:px-6">
        {when.weekday}, {when.month} {when.day} · {when.time} · {session.venue} · {when.fee} each
      </p>

      {rows.data.length === 0 ? (
        <p className="border-t border-rule px-5 py-8 text-center font-semibold text-foreground md:px-6">All settled. Nothing to check.</p>
      ) : (
        <>
          <Group title="To check" hint="Match each against the Zelle app, then confirm." rows={reported} sessionId={session.id} actionable />
          <Group title="Sent back" hint="Waiting for the member to report again." rows={rejected} sessionId={session.id} />
          <Group title="Said yes, not reported" hint="Nothing to confirm yet." rows={silent} sessionId={session.id} />
        </>
      )}
    </div>
  )
}

function Group({ title, hint, rows, sessionId, actionable = false }: { title: string; hint: string; rows: UnsettledRow[]; sessionId: string; actionable?: boolean }) {
  if (rows.length === 0) return null
  return (
    <section aria-label={title} className="border-t border-rule">
      <header className="flex flex-wrap items-baseline justify-between gap-2 bg-paper/70 px-5 py-3 md:px-6">
        <h3 className="label text-foreground">{title}</h3>
        <span className="text-xs text-muted-foreground">{hint}</span>
      </header>
      <ul className="divide-y divide-rule">
        {rows.map((row) => (
          <Row key={row.clerkUserId} row={row} sessionId={sessionId} actionable={actionable} />
        ))}
      </ul>
    </section>
  )
}

function Row({ row, sessionId, actionable }: { row: UnsettledRow; sessionId: string; actionable: boolean }) {
  const apiFetch = useApiFetch()
  const queryClient = useQueryClient()
  const reasonId = useId()
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState("")
  const [problem, setProblem] = useState<string | null>(null)
  const name = row.displayName ?? "Unnamed member"
  const base = `/sessions/${sessionId}/payments/${encodeURIComponent(row.clerkUserId)}` as const

  const act = useMutation({
    mutationFn: (verdict: { kind: "confirm" } | { kind: "reject"; reason: string }) =>
      verdict.kind === "confirm"
        ? apiFetch<void>(`${base}/confirm`, { method: "POST" })
        : apiFetch<void>(`${base}/reject`, { method: "POST", json: { reason: verdict.reason } }),
    onMutate: () => setProblem(null),
    onError: (error) => setProblem(actionProblem(error)),
    onSuccess: () => setRejecting(false),
    onSettled: () => queryClient.invalidateQueries({ queryKey: paymentsKey(sessionId) }),
  })

  return (
    <li className="grid gap-3 px-5 py-4 md:grid-cols-[1fr_auto] md:items-center md:px-6">
      <div className="min-w-0">
        <p className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <span className="font-semibold text-foreground">{name}</span>
          <span className="figures text-sm text-muted-foreground">{formatDollars(row.amountCents)}</span>
        </p>
        {row.senderName || row.reference ? (
          <dl className="mt-1 flex flex-wrap gap-x-5 gap-y-1 text-sm">
            <div className="flex gap-1.5">
              <dt className="text-muted-foreground">From</dt>
              <dd className="text-foreground">{row.senderName}</dd>
            </div>
            <div className="flex gap-1.5">
              <dt className="text-muted-foreground">Ref</dt>
              <dd className="figures break-all text-foreground">{row.reference}</dd>
            </div>
          </dl>
        ) : null}
        {row.rejectionReason ? <p className="mt-1 border-l-2 border-live pl-2 text-sm text-muted-foreground">{row.rejectionReason}</p> : null}
      </div>

      {actionable && !rejecting ? (
        <div className="flex gap-2">
          <button
            type="button"
            disabled={act.isPending}
            onClick={() => act.mutate({ kind: "confirm" })}
            aria-label={`Confirm ${name}'s payment`}
            className="min-h-11 flex-1 rounded-md bg-primary px-5 text-sm font-bold uppercase tracking-[0.12em] text-primary-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait disabled:opacity-70 md:flex-none"
          >
            Confirm
          </button>
          <button
            type="button"
            disabled={act.isPending}
            onClick={() => setRejecting(true)}
            aria-label={`Send back ${name}'s payment`}
            className="min-h-11 rounded-md px-4 text-sm font-semibold text-muted-foreground ring-1 ring-rule hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait"
          >
            Send back
          </button>
        </div>
      ) : null}

      {actionable && rejecting ? (
        <form
          className="grid gap-2 md:col-span-2 md:grid-cols-[1fr_auto]"
          onSubmit={(e) => {
            e.preventDefault()
            if (!reason.trim()) {
              setProblem("Say why, so the member knows what to fix.")
              return
            }
            act.mutate({ kind: "reject", reason: reason.trim() })
          }}
        >
          <label htmlFor={reasonId} className="label text-muted-foreground md:col-span-2">
            Why it can&apos;t be confirmed (the member sees this)
          </label>
          <input
            id={reasonId}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={REJECTION_REASON_MAX}
            autoFocus
            className="min-h-11 rounded-md bg-background px-3 text-foreground ring-1 ring-rule focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={act.isPending}
              className="min-h-11 flex-1 rounded-md bg-live px-4 text-sm font-bold uppercase tracking-[0.12em] text-primary-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait"
            >
              Send back
            </button>
            <button
              type="button"
              onClick={() => {
                setRejecting(false)
                setProblem(null)
              }}
              className="min-h-11 rounded-md px-4 text-sm font-semibold text-muted-foreground ring-1 ring-rule hover:text-foreground"
            >
              Cancel
            </button>
          </div>
        </form>
      ) : null}

      {problem ? (
        <p role="status" className="text-sm text-live md:col-span-2">
          {problem}
        </p>
      ) : null}
    </li>
  )
}

function Figure({ label, value, tone = "text-foreground" }: { label: string; value: number | string; tone?: string }) {
  return (
    <div className="flex flex-col gap-1 bg-card px-5 py-4 md:px-6">
      <dt className="label text-muted-foreground">{label}</dt>
      <dd className={cn("display figures text-4xl", tone)}>{value}</dd>
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return <p className="mt-10 rounded-lg bg-card p-5 text-muted-foreground ring-1 ring-rule md:p-8">{children}</p>
}

function Skeleton() {
  return <span aria-label="Loading" className="mt-10 block h-48 w-full animate-pulse rounded-lg bg-muted motion-reduce:animate-none" />
}

function problemText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Sign in as a club admin to use this page."
    if (error.status === 403) return "This page is for the club's admins."
    if (error.status === 400 || error.status === 404) return "Sessions aren't open at this address yet."
  }
  return "Couldn't reach the club right now."
}

function actionProblem(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return "This payment changed meanwhile. The list has been refreshed."
    if (error.status === 404) return "That payment is gone. The list has been refreshed."
    if (error.status === 400) return "The reason needs some text, up to 500 characters."
  }
  return "Couldn't save that. Try again."
}

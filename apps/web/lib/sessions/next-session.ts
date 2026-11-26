// Picking "this Sunday" on the member home (P1-014). The API lists one ISO
// week at a time (`GET /sessions?week=2026-W48`) and leaves "this week" to the
// device, which knows the member's wall clock. These helpers are pure so the
// browser tests can check them without a server.

export type SessionStatus = "Scheduled" | "Cancelled"
export type RsvpAnswer = "Yes" | "Maybe" | "No"

export type SessionSummary = {
  id: string
  date: string // YYYY-MM-DD, club wall clock
  startTime: string // HH:mm:ss, club wall clock
  venue: string
  feeCents: number
  status: SessionStatus
}

export type SessionDetails = SessionSummary & {
  rsvpCounts: { yes: number; no: number; maybe: number }
  myRsvp: RsvpAnswer | null
  myPayment: { status: "Reported" | "Confirmed" | "Rejected"; amountCents: number; rejectionReason: string | null } | null
}

/** The ISO 8601 week a local date falls in, e.g. `2026-W48`. Weeks run Monday to Sunday. */
export function isoWeekOf(date: Date): string {
  // Work in UTC on the local calendar date, so daylight saving can't shift a day.
  const day = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()))
  const weekday = day.getUTCDay() || 7 // Monday 1 … Sunday 7
  day.setUTCDate(day.getUTCDate() + 4 - weekday) // the Thursday decides the year
  const year = day.getUTCFullYear()
  const week = Math.ceil(((day.getTime() - Date.UTC(year, 0, 1)) / 86_400_000 + 1) / 7)
  return `${year}-W${String(week).padStart(2, "0")}`
}

/** The ISO week seven days after `date`. */
export function isoWeekAfter(date: Date): string {
  return isoWeekOf(new Date(date.getFullYear(), date.getMonth(), date.getDate() + 7))
}

/** A session's start on the local clock. */
export function startOf(session: Pick<SessionSummary, "date" | "startTime">): Date {
  const [y, m, d] = session.date.split("-").map(Number)
  const [hh, mm] = session.startTime.split(":").map(Number)
  return new Date(y, m - 1, d, hh, mm)
}

/**
 * The first session that hasn't started yet, earliest first. Cancelled ones
 * count: a member needs to see that Sunday is off. `undefined` when every
 * session in the list has started (or there are none).
 */
export function nextSession<T extends SessionSummary>(sessions: readonly T[], now: Date): T | undefined {
  return [...sessions]
    .filter((s) => startOf(s).getTime() > now.getTime())
    .sort((a, b) => startOf(a).getTime() - startOf(b).getTime())[0]
}

/** True when the value looks like the API's session list; anything else is treated as an error. */
export function isSessionList(value: unknown): value is SessionSummary[] {
  return (
    Array.isArray(value) &&
    value.every((s) => s && typeof s === "object" && typeof s.id === "string" && typeof s.date === "string" && typeof s.startTime === "string")
  )
}

/** "Sunday", "November 30", "7:00 AM", "$15" — the card's pieces. */
export function describeSession(session: Pick<SessionSummary, "date" | "startTime" | "feeCents">) {
  const start = startOf(session)
  return {
    weekday: start.toLocaleDateString("en-US", { weekday: "long" }),
    month: start.toLocaleDateString("en-US", { month: "short" }),
    day: String(start.getDate()),
    time: start.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" }),
    fee: session.feeCents === 0 ? "Free" : formatDollars(session.feeCents),
    dateTimeAttr: `${session.date}T${session.startTime.slice(0, 5)}`,
  }
}

export function formatDollars(cents: number): string {
  return cents % 100 === 0 ? `$${cents / 100}` : `$${(cents / 100).toFixed(2)}`
}

/** The counts after the member changes their answer from `from` to `to`, for an optimistic update. */
export function recount(counts: SessionDetails["rsvpCounts"], from: RsvpAnswer | null, to: RsvpAnswer): SessionDetails["rsvpCounts"] {
  if (from === to) return counts
  const next = { ...counts }
  if (from) next[key(from)] = Math.max(0, next[key(from)] - 1)
  next[key(to)] += 1
  return next
}

function key(answer: RsvpAnswer): keyof SessionDetails["rsvpCounts"] {
  return answer === "Yes" ? "yes" : answer === "No" ? "no" : "maybe"
}

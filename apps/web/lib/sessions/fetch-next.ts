import type { ApiFetch } from "@/lib/api/client"
import { isSessionList, isoWeekAfter, isoWeekOf, nextSession, type SessionSummary } from "@/lib/sessions/next-session"

/** The query key for the next session, shared by the home card and the admin's roll call. */
export const NEXT_SESSION_KEY = ["sessions", "next"] as const

/**
 * The next session still to come on this device's clock (P1-014): this ISO
 * week first, then next week once this week's sessions have started. Null
 * when neither week has one.
 */
export async function fetchNextSession(apiFetch: ApiFetch): Promise<SessionSummary | null> {
  const now = new Date()
  for (const week of [isoWeekOf(now), isoWeekAfter(now)]) {
    const list = await apiFetch<unknown>(`/sessions?week=${week}`)
    if (!isSessionList(list)) throw new Error("Unexpected session list")
    const next = nextSession(list, now)
    if (next) return next
  }
  return null
}

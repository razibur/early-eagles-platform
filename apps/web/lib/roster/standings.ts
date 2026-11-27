// The club's standings for the year (P1-019), as GET /leaderboard?period=year
// returns them (P1-011): every current member, most attended first, then most
// paid, then by name, unnamed last. Pure helpers so the sorting is testable
// without a browser.

export type StandingRow = {
  clerkUserId: string
  displayName: string | null
  imageUrl: string | null
  sessionsAttended: number
  timesPaid: number
  /** ISO date ("2026-11-22") of the last session they attended, or null. */
  lastPlayed: string | null
}

export type StandingKey = "standing" | "name" | "sessionsAttended" | "timesPaid" | "lastPlayed"
export type SortDirection = "ascending" | "descending"

/** A row with its place in the API's order, which is the standing. */
export type RankedRow = StandingRow & { rank: number }

export function isStandingList(value: unknown): value is StandingRow[] {
  return (
    Array.isArray(value) &&
    value.every((r) => typeof r === "object" && r !== null && typeof r.clerkUserId === "string" && typeof r.sessionsAttended === "number")
  )
}

/**
 * Ranks rows in the API's order. Members level on both counts share a rank
 * (1, 2, 2, 4), so the name order the API breaks ties with isn't a placing.
 */
export function rank(rows: StandingRow[]): RankedRow[] {
  return rows.map((row, index) => {
    let first = index
    while (first > 0 && rows[first - 1].sessionsAttended === row.sessionsAttended && rows[first - 1].timesPaid === row.timesPaid) first--
    return { ...row, rank: first + 1 }
  })
}

/** The direction a column sorts in when first chosen: counts and dates high first, names A to Z. */
export function firstDirection(key: StandingKey): SortDirection {
  return key === "name" || key === "standing" ? "ascending" : "descending"
}

/**
 * Sorts ranked rows by a column. Ties keep the standing order, and unnamed
 * members and members who haven't played stay last in either direction.
 */
export function sortStandings(rows: RankedRow[], key: StandingKey, direction: SortDirection): RankedRow[] {
  const sign = direction === "ascending" ? 1 : -1
  // Array sort is stable, so ties keep the standing order they arrive in.
  return [...rows].sort((a, b) => compare(a, b, key, sign))
}

function compare(a: RankedRow, b: RankedRow, key: StandingKey, sign: number): number {
  switch (key) {
    case "standing":
      return sign * (a.rank - b.rank)
    case "sessionsAttended":
      return sign * (a.sessionsAttended - b.sessionsAttended)
    case "timesPaid":
      return sign * (a.timesPaid - b.timesPaid)
    case "name":
      return nullsLast(a.displayName, b.displayName) ?? sign * a.displayName!.localeCompare(b.displayName!, undefined, { sensitivity: "base" })
    case "lastPlayed":
      // ISO dates sort as strings.
      return nullsLast(a.lastPlayed, b.lastPlayed) ?? sign * a.lastPlayed!.localeCompare(b.lastPlayed!)
  }
}

function nullsLast(a: string | null, b: string | null): number | null {
  if (a === null && b === null) return 0
  if (a === null) return 1
  if (b === null) return -1
  return null
}

/** "Nov 22" for an ISO date, read as a calendar date (no time zone shift). */
export function formatPlayed(iso: string): string {
  const [year, month, day] = iso.split("-").map(Number)
  return new Date(year, month - 1, day).toLocaleDateString("en-US", { month: "short", day: "numeric" })
}

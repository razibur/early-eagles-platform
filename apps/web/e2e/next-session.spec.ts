import { expect, test } from "@playwright/test"

import {
  describeSession,
  isSessionList,
  isoWeekAfter,
  isoWeekOf,
  nextSession,
  recount,
  type SessionSummary,
} from "../lib/sessions/next-session"

// P1-014 — choosing "this Sunday" from the API's week lists. Pure; run once.
test.describe("next session helpers", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  test("ISO weeks run Monday to Sunday and the Thursday decides the year", () => {
    expect(isoWeekOf(new Date(2026, 10, 23))).toBe("2026-W48") // Monday
    expect(isoWeekOf(new Date(2026, 10, 29))).toBe("2026-W48") // Sunday
    expect(isoWeekOf(new Date(2026, 10, 30))).toBe("2026-W49") // next Monday
    expect(isoWeekOf(new Date(2027, 0, 1))).toBe("2026-W53") // Friday, belongs to 2026
    expect(isoWeekOf(new Date(2024, 11, 30))).toBe("2025-W01") // Monday, belongs to 2025
    expect(isoWeekOf(new Date(2026, 0, 1))).toBe("2026-W01")
  })

  test("the week after crosses a year boundary", () => {
    expect(isoWeekAfter(new Date(2026, 11, 28))).toBe("2027-W01")
  })

  const session = (id: string, date: string, startTime = "07:00:00", status: SessionSummary["status"] = "Scheduled"): SessionSummary => ({
    id,
    date,
    startTime,
    venue: "Gym",
    feeCents: 1500,
    status,
  })

  test("the next session is the earliest that hasn't started", () => {
    const now = new Date(2026, 10, 29, 6, 59)
    const list = [session("later", "2026-11-29", "09:00:00"), session("sunday", "2026-11-29"), session("saturday", "2026-11-28")]
    expect(nextSession(list, now)?.id).toBe("sunday")
  })

  test("a session that started at this minute is past", () => {
    expect(nextSession([session("sunday", "2026-11-29")], new Date(2026, 10, 29, 7, 0))).toBeUndefined()
  })

  test("a cancelled session still counts, so members see it's off", () => {
    expect(nextSession([session("off", "2026-11-29", "07:00:00", "Cancelled")], new Date(2026, 10, 25))?.id).toBe("off")
  })

  test("only a list of sessions is accepted", () => {
    expect(isSessionList([session("a", "2026-11-29")])).toBe(true)
    expect(isSessionList([])).toBe(true)
    expect(isSessionList({ method: "GET" })).toBe(false)
    expect(isSessionList([{ id: 1 }])).toBe(false)
  })

  test("the card's pieces", () => {
    expect(describeSession({ date: "2026-11-29", startTime: "07:30:00", feeCents: 1500 })).toEqual({
      weekday: "Sunday",
      month: "Nov",
      day: "29",
      time: "7:30 AM",
      fee: "$15",
      dateTimeAttr: "2026-11-29T07:30",
    })
    expect(describeSession({ date: "2026-11-29", startTime: "07:30:00", feeCents: 0 }).fee).toBe("Free")
    expect(describeSession({ date: "2026-11-29", startTime: "07:30:00", feeCents: 1250 }).fee).toBe("$12.50")
  })

  test("changing an answer moves one count", () => {
    const counts = { yes: 3, no: 1, maybe: 2 }
    expect(recount(counts, null, "Yes")).toEqual({ yes: 4, no: 1, maybe: 2 })
    expect(recount(counts, "Maybe", "No")).toEqual({ yes: 3, no: 2, maybe: 1 })
    expect(recount(counts, "Yes", "Yes")).toBe(counts)
  })
})

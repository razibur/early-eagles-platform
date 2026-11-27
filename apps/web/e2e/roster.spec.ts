import { expect, test, type Page } from "@playwright/test"

import { firstDirection, formatPlayed, rank, sortStandings, type StandingRow } from "../lib/roster/standings"

// P1-019 / P1-018 — the roster as the year's standings. GET /leaderboard and
// /me are stubbed; rows arrive in the API's order (most played, most paid,
// name, unnamed last).

const row = (id: string, displayName: string | null, sessionsAttended: number, timesPaid: number, lastPlayed: string | null): StandingRow => ({
  clerkUserId: id,
  displayName,
  imageUrl: null,
  sessionsAttended,
  timesPaid,
  lastPlayed,
})

const board = [
  row("u1", "Rahim Uddin", 40, 38, "2026-11-22"),
  row("u2", "Abdul Karim", 31, 31, "2026-11-22"),
  row("u3", "Sami Khan", 31, 31, "2026-11-15"),
  row("u4", "Faisal Ahmed", 12, 14, "2026-08-30"),
  row("u5", null, 0, 0, null),
]

async function open(page: Page, leaderboard: { status: number; body: unknown } = { status: 200, body: board }, me = "u3") {
  await page.route("**/api/me", (route) => route.fulfill({ json: { userId: me, sessionId: "s", organization: null, tenantRole: "member" } }))
  await page.route(
    (url) => url.pathname === "/api/leaderboard",
    (route) =>
      route.fulfill({
        status: leaderboard.status,
        json: leaderboard.body,
        contentType: leaderboard.status < 400 ? "application/json" : "application/problem+json",
      }),
  )
  await page.goto("/roster")
  return page.getByRole("table")
}

const names = (table: ReturnType<Page["getByRole"]>) => table.locator("tbody th").allTextContents()

test("every member in standing order, ties sharing a place, you marked", async ({ page }) => {
  const table = await open(page)

  await expect(page.getByRole("heading", { level: 1, name: "The standings" })).toBeVisible()
  await expect(table.locator("tbody tr")).toHaveCount(5)
  expect(await table.locator("tbody td:first-child").allTextContents()).toEqual(["1", "2", "2", "4", "5"])
  expect(await names(table)).toEqual(["Rahim Uddin", "Abdul Karim", "Sami KhanYou", "Faisal Ahmed", "Unnamed member"])
  await expect(table.locator('tr[aria-current="true"]')).toContainText("Sami Khan")
  await expect(table.getByRole("columnheader", { name: "Standing" })).toHaveAttribute("aria-sort", "ascending")
})

test("tapping a column sorts by it, and tapping again flips it", async ({ page }) => {
  const table = await open(page)

  await table.getByRole("button", { name: "Member" }).click()
  await expect(table.getByRole("columnheader", { name: "Member" })).toHaveAttribute("aria-sort", "ascending")
  await expect(table.getByRole("columnheader", { name: "Standing" })).toHaveAttribute("aria-sort", "none")
  expect(await names(table)).toEqual(["Abdul Karim", "Faisal Ahmed", "Rahim Uddin", "Sami KhanYou", "Unnamed member"])

  await table.getByRole("button", { name: "Member" }).click()
  await expect(table.getByRole("columnheader", { name: "Member" })).toHaveAttribute("aria-sort", "descending")
  // Unnamed stays last either way.
  expect(await names(table)).toEqual(["Sami KhanYou", "Rahim Uddin", "Faisal Ahmed", "Abdul Karim", "Unnamed member"])

  await table.getByRole("button", { name: "Paid" }).click()
  await expect(table.getByRole("columnheader", { name: "Paid" })).toHaveAttribute("aria-sort", "descending")
  expect((await names(table))[0]).toBe("Rahim Uddin")
})

test("the last-played column shows dates from tablet width up", async ({ page }, testInfo) => {
  const table = await open(page)
  const lastPlayed = table.getByRole("columnheader", { name: "Last played" })
  if (testInfo.project.name === "phone") {
    await expect(lastPlayed).toBeHidden()
  } else {
    await expect(lastPlayed).toBeVisible()
    await expect(table.locator("tbody tr").first()).toContainText("Nov 22")
    await expect(table.locator("tbody tr").last()).toContainText("—")
  }
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  expect(overflow, "horizontal overflow in px").toBeLessThanOrEqual(0)
})

test("an empty club says so", async ({ page }) => {
  await open(page, { status: 200, body: [] })
  await expect(page.getByText("No members on the roster yet.")).toBeVisible()
  await expect(page.getByRole("table")).toHaveCount(0)
})

for (const [status, message] of [
  [401, "Sign in to see the roster."],
  [403, "You're not on this club's roster yet. Your club's admin adds you."],
  [404, "The roster isn't open at this address yet."],
  [500, "Couldn't reach the club right now."],
] as const) {
  test(`a ${status} from the API is explained`, async ({ page }) => {
    await open(page, { status, body: { status } })
    await expect(page.getByText(message)).toBeVisible()
  })
}

test.describe("standings maths", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  test("members level on both counts share a rank", () => {
    expect(rank(board).map((r) => r.rank)).toEqual([1, 2, 2, 4, 5])
    expect(rank([row("a", "A", 3, 2, null), row("b", "B", 3, 1, null)]).map((r) => r.rank)).toEqual([1, 2])
  })

  test("sorting keeps nulls last and ties in standing order", () => {
    const ranked = rank(board)
    expect(sortStandings(ranked, "lastPlayed", "descending").map((r) => r.clerkUserId)).toEqual(["u1", "u2", "u3", "u4", "u5"])
    expect(sortStandings(ranked, "lastPlayed", "ascending").map((r) => r.clerkUserId)).toEqual(["u4", "u3", "u1", "u2", "u5"])
    expect(sortStandings(ranked, "sessionsAttended", "ascending").map((r) => r.clerkUserId)).toEqual(["u5", "u4", "u2", "u3", "u1"])
    expect(sortStandings(ranked, "standing", "descending").map((r) => r.clerkUserId)).toEqual(["u5", "u4", "u2", "u3", "u1"])
  })

  test("counts and dates start high, names start at A", () => {
    expect(firstDirection("sessionsAttended")).toBe("descending")
    expect(firstDirection("lastPlayed")).toBe("descending")
    expect(firstDirection("name")).toBe("ascending")
  })

  test("a played date is read as a calendar date", () => {
    expect(formatPlayed("2026-11-01")).toBe("Nov 1")
  })
})

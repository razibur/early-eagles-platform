import { expect, test, type Page, type Route } from "@playwright/test"

import { groupRollCall, type RsvpGridRow } from "../components/admin/roll-call-panel"

// P1-033.1 — the roll call on /admin: every member against the next session,
// who to chase first. The API is stubbed and the clock fixed on Wednesday
// 25 November 2026, so the next session is Sunday the 29th (2026-W48).

const NOW = new Date(2026, 10, 25, 10, 0)
const ID = "5e550000-0000-0000-0000-000000000029"

const summary = (over: Record<string, unknown> = {}) => ({
  id: ID,
  date: "2026-11-29",
  startTime: "07:00:00",
  venue: "Eastside Rec Center",
  feeCents: 1500,
  status: "Scheduled",
  ...over,
})

const row = (id: string, displayName: string | null, response: RsvpGridRow["response"]): RsvpGridRow => ({
  clerkUserId: id,
  displayName,
  imageUrl: null,
  response,
})

const roster = [
  row("u1", "Abdul Karim", "Yes"),
  row("u2", "Faisal Ahmed", null),
  row("u3", "Rahim Uddin", "Yes"),
  row("u4", "Sami Khan", "Maybe"),
  row("u5", "Tariq Hasan", "No"),
  row("u6", null, null),
]

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, json: body, contentType: status < 400 ? "application/json" : "application/problem+json" })
}

async function open(page: Page, options: { sessions?: unknown[]; grid?: unknown[]; gridStatus?: number } = {}) {
  await page.clock.setFixedTime(NOW)
  await page.route("**/api/me", (route) => json(route, 200, { userId: "user_admin", sessionId: "s", organization: null, tenantRole: "tenant-admin" }))
  await page.route(
    (url) => url.pathname === "/api/sessions",
    (route) => {
      const week = new URL(route.request().url()).searchParams.get("week")
      return json(route, 200, week === "2026-W48" ? (options.sessions ?? [summary()]) : [])
    },
  )
  await page.route(`**/api/sessions/${ID}/rsvps`, (route) =>
    options.gridStatus ? json(route, options.gridStatus, { status: options.gridStatus }) : json(route, 200, options.grid ?? roster),
  )
  // The dues desk below asks too; nothing to settle here.
  await page.route(`**/api/sessions/${ID}/payments`, (route) => json(route, 200, []))
  await page.goto("/admin")
  return page.getByRole("region", { name: "Roll call" })
}

test("the next Sunday's roll call puts who to chase first", async ({ page }) => {
  const panel = await open(page)

  await expect(panel.getByRole("heading", { name: "Roll call" })).toBeVisible()
  await expect(panel.getByText("Sunday, Nov 29 · 7:00 AM · Eastside Rec Center")).toBeVisible()
  await expect(panel.locator("dt", { hasText: "In" }).locator("+ dd")).toHaveText("2")
  await expect(panel.locator("dt", { hasText: "Maybe" }).locator("+ dd")).toHaveText("1")
  await expect(panel.locator("dt", { hasText: "Out" }).locator("+ dd")).toHaveText("1")
  await expect(panel.locator("dt", { hasText: "No answer" }).locator("+ dd")).toHaveText("2")

  const chase = panel.getByRole("region", { name: "No answer yet" })
  await expect(chase.getByRole("listitem")).toHaveText(["Faisal Ahmed", "Unnamed member"])
  await expect(panel.getByRole("region", { name: "In" }).getByRole("listitem")).toHaveText(["Abdul Karim", "Rahim Uddin"])
  await expect(panel.getByRole("region", { name: "Maybe" }).getByRole("listitem")).toHaveText(["Sami Khan"])
  await expect(panel.getByRole("region", { name: "Out" }).getByRole("listitem")).toHaveText(["Tariq Hasan"])

  // The chase list comes first.
  const titles = await panel.getByRole("heading", { level: 3 }).allTextContents()
  expect(titles.map((t) => t.split("·")[0].trim())).toEqual(["No answer yet", "In", "Maybe", "Out"])
})

test("when everyone has answered there is nobody to chase", async ({ page }) => {
  const panel = await open(page, { grid: [row("u1", "Abdul Karim", "Yes"), row("u5", "Tariq Hasan", "No")] })

  await expect(panel.getByRole("region", { name: "In" })).toBeVisible()
  await expect(panel.getByRole("region", { name: "No answer yet" })).toHaveCount(0)
  await expect(panel.getByRole("region", { name: "Maybe" })).toHaveCount(0)
  await expect(panel.locator("dt", { hasText: "No answer" }).locator("+ dd")).toHaveText("0")
})

test("a called-off Sunday says so instead of listing anyone", async ({ page }) => {
  const panel = await open(page, { sessions: [summary({ status: "Cancelled" })] })

  await expect(panel.getByText("Sunday, Nov 29 · 7:00 AM · Eastside Rec Center: called off. Nobody to chase.")).toBeVisible()
  await expect(panel.getByRole("listitem")).toHaveCount(0)
})

test("no session this week or next says so", async ({ page }) => {
  const panel = await open(page, { sessions: [] })
  await expect(panel.getByText("No session on the board this week or next.")).toBeVisible()
})

test("an empty roster says so", async ({ page }) => {
  const panel = await open(page, { grid: [] })
  await expect(panel.getByText("No members on the roster yet.")).toBeVisible()
})

test("a refusal from the API is explained", async ({ page }) => {
  const panel = await open(page, { gridStatus: 403 })
  await expect(panel.getByText("This page is for the club's admins.")).toBeVisible()
})

test.describe("grouping", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  test("splits by answer and keeps the API's name order", () => {
    const groups = groupRollCall(roster)
    expect(groups.unanswered.map((r) => r.clerkUserId)).toEqual(["u2", "u6"])
    expect(groups.yes.map((r) => r.clerkUserId)).toEqual(["u1", "u3"])
    expect(groups.maybe.map((r) => r.clerkUserId)).toEqual(["u4"])
    expect(groups.no.map((r) => r.clerkUserId)).toEqual(["u5"])
  })
})

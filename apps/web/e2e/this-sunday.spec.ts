import { expect, test, type Page, type Route } from "@playwright/test"

// P1-014 — the "This Sunday" card on /. The API is stubbed in the browser and
// the clock fixed on Wednesday 25 November 2026, 10:00, so this week is
// 2026-W48 and its Sunday is the 29th.

const NOW = new Date(2026, 10, 25, 10, 0)
const SUNDAY_ID = "5e550000-0000-0000-0000-000000000029"

const summary = (over: Record<string, unknown> = {}) => ({
  id: SUNDAY_ID,
  date: "2026-11-29",
  startTime: "07:00:00",
  venue: "Eastside Rec Center",
  feeCents: 1500,
  status: "Scheduled",
  ...over,
})

const details = (over: Record<string, unknown> = {}) => ({
  ...summary(),
  rsvpCounts: { yes: 12, no: 2, maybe: 3 },
  myRsvp: null,
  myPayment: null,
  ...over,
})

type Stubs = {
  weeks?: Record<string, { status: number; body: unknown }>
  details?: { status: number; body: unknown }
  rsvp?: (route: Route) => Promise<void>
}

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, json: body, contentType: status < 400 ? "application/json" : "application/problem+json" })
}

async function open(page: Page, stubs: Stubs) {
  const weeksAsked: string[] = []
  const rsvps: unknown[] = []
  await page.clock.setFixedTime(NOW)
  await page.route(
    (url) => url.pathname === "/api/sessions",
    (route) => {
      const week = new URL(route.request().url()).searchParams.get("week") ?? ""
      weeksAsked.push(week)
      const stub = stubs.weeks?.[week] ?? { status: 200, body: [] }
      return json(route, stub.status, stub.body)
    },
  )
  await page.route(`**/api/sessions/${SUNDAY_ID}`, (route) => {
    const stub = stubs.details ?? { status: 200, body: details() }
    return json(route, stub.status, stub.body)
  })
  await page.route(`**/api/sessions/${SUNDAY_ID}/rsvp`, async (route) => {
    rsvps.push(route.request().postDataJSON())
    if (stubs.rsvp) return stubs.rsvp(route)
    return route.fulfill({ status: 204 })
  })
  await page.goto("/")
  return { weeksAsked, rsvps }
}

test("shows this week's Sunday with the tally and the three answers", async ({ page }) => {
  const { weeksAsked } = await open(page, { weeks: { "2026-W48": { status: 200, body: [summary()] } } })

  const card = page.getByRole("article", { name: /29/ })
  await expect(card.getByText("This Sunday")).toBeVisible()
  await expect(card.getByText("7:00 AM")).toBeVisible()
  await expect(card.getByText("Eastside Rec Center")).toBeVisible()
  await expect(card.getByText("$15", { exact: true })).toBeVisible()
  await expect(card.getByText("12", { exact: true })).toBeVisible()
  await expect(card.getByText("Are you playing?")).toBeVisible()
  for (const name of [/I'm in/, /Maybe/, /Can't make it/]) {
    await expect(card.getByRole("button", { name })).toHaveAttribute("aria-pressed", "false")
  }
  expect(weeksAsked).toEqual(["2026-W48"])
})

test("tapping an answer sends it, marks it and moves the tally", async ({ page }) => {
  const { rsvps } = await open(page, { weeks: { "2026-W48": { status: 200, body: [summary()] } } })

  const card = page.getByRole("article", { name: /29/ })
  // After the PUT the card refetches; answer with the saved state.
  await page.route(`**/api/sessions/${SUNDAY_ID}`, (route) => json(route, 200, details({ myRsvp: "Yes", rsvpCounts: { yes: 13, no: 2, maybe: 3 } })))
  await card.getByRole("button", { name: /I'm in/ }).click()

  await expect(card.getByRole("button", { name: /I'm in/ })).toHaveAttribute("aria-pressed", "true")
  await expect(card.getByText("13", { exact: true })).toBeVisible()
  await expect(card.getByText(/Your answer/)).toBeVisible()
  expect(rsvps).toEqual([{ response: "Yes" }])
})

test("a closed session puts the answer back and says why", async ({ page }) => {
  await open(page, {
    weeks: { "2026-W48": { status: 200, body: [summary()] } },
    details: { status: 200, body: details({ myRsvp: "Maybe" }) },
    rsvp: (route) => json(route, 409, { title: "Conflict", status: 409 }),
  })

  const card = page.getByRole("article", { name: /29/ })
  await card.getByRole("button", { name: /Can't make it/ }).click()

  await expect(card.getByText(/Answers are closed/)).toBeVisible()
  await expect(card.getByRole("button", { name: /Maybe/ })).toHaveAttribute("aria-pressed", "true")
  await expect(card.getByRole("button", { name: /Can't make it/ })).toHaveAttribute("aria-pressed", "false")
})

test("a cancelled Sunday is shown as called off, with no answers to give", async ({ page }) => {
  await open(page, {
    weeks: { "2026-W48": { status: 200, body: [summary({ status: "Cancelled" })] } },
    details: { status: 200, body: details({ status: "Cancelled" }) },
  })

  const card = page.getByRole("article", { name: /29/ })
  await expect(card.getByText("Called off")).toBeVisible()
  await expect(card.getByRole("button", { name: /I'm in/ })).toHaveCount(0)
})

test("once this week's sessions have started, next week's is shown", async ({ page }) => {
  const { weeksAsked } = await open(page, {
    weeks: {
      "2026-W48": { status: 200, body: [summary({ id: "past", date: "2026-11-24" })] },
      "2026-W49": { status: 200, body: [summary({ date: "2026-12-06" })] },
    },
    details: { status: 200, body: details({ date: "2026-12-06" }) },
  })

  await expect(page.getByRole("article", { name: /6/ }).getByText("Dec")).toBeVisible()
  expect(weeksAsked).toEqual(["2026-W48", "2026-W49"])
})

test("nothing scheduled says so", async ({ page }) => {
  await open(page, {})
  await expect(page.getByText("No session on the board")).toBeVisible()
})

for (const [status, text] of [
  [401, "Sign in to see this Sunday."],
  [403, /not on this club's roster yet/],
  [404, "Sessions aren't open at this address yet."],
  [500, "Couldn't reach the club right now."],
] as const) {
  test(`a ${status} from the API is explained`, async ({ page }) => {
    await open(page, { weeks: { "2026-W48": { status, body: { status } } } })
    await expect(page.getByText(text)).toBeVisible()
  })
}

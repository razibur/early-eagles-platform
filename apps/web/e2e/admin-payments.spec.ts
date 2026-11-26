import { expect, test, type Page, type Route } from "@playwright/test"

// P1-017 — the admin's dues desk on /admin. The API is stubbed and the clock
// fixed on Monday 30 November 2026, so last week is 2026-W48 (Sunday 29th,
// just played), this week W49 and next week W50.

const NOW = new Date(2026, 10, 30, 10, 0)
const PLAYED = "5e550000-0000-0000-0000-000000000029"
const NEXT = "5e550000-0000-0000-0000-000000001206"

const summary = (id: string, date: string, over: Record<string, unknown> = {}) => ({
  id,
  date,
  startTime: "07:00:00",
  venue: "Eastside Rec Center",
  feeCents: 1500,
  status: "Scheduled",
  ...over,
})

const rows = [
  { clerkUserId: "user_rahim", displayName: "Rahim Uddin", amountCents: 1500, status: "Reported", senderName: "Rahim U", reference: "ZL-88412", rejectionReason: null },
  { clerkUserId: "user_karim", displayName: "Karim Ali", amountCents: 1500, status: "Rejected", senderName: "K Ali", reference: "ZL-1", rejectionReason: "No transfer with that reference." },
  { clerkUserId: "user_sami", displayName: null, amountCents: 1500, status: null, senderName: null, reference: null, rejectionReason: null },
]

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, json: body, contentType: status < 400 ? "application/json" : "application/problem+json" })
}

type Options = {
  role?: string | null
  meStatus?: number
  weeks?: Record<string, unknown[]>
  payments?: Record<string, unknown>
  verdict?: (route: Route) => Promise<void>
}

async function open(page: Page, options: Options = {}) {
  const calls: { url: string; body: unknown }[] = []
  const payments: Record<string, unknown> = { [PLAYED]: rows, [NEXT]: [], ...options.payments }
  await page.clock.setFixedTime(NOW)
  await page.route("**/api/me", (route) =>
    options.meStatus
      ? json(route, options.meStatus, { status: options.meStatus })
      : json(route, 200, { userId: "user_admin", sessionId: "s", organization: null, tenantRole: options.role === undefined ? "tenant-admin" : options.role }),
  )
  await page.route(
    (url) => url.pathname === "/api/sessions",
    (route) => {
      const week = new URL(route.request().url()).searchParams.get("week") ?? ""
      const weeks = options.weeks ?? { "2026-W48": [summary(PLAYED, "2026-11-29")], "2026-W50": [summary(NEXT, "2026-12-06")] }
      return json(route, 200, weeks[week] ?? [])
    },
  )
  await page.route(/\/api\/sessions\/[^/]+\/payments$/, (route) => {
    const id = new URL(route.request().url()).pathname.split("/")[3]
    return json(route, 200, payments[id] ?? [])
  })
  await page.route(/\/api\/sessions\/[^/]+\/payments\/[^/]+\/(confirm|reject)$/, async (route) => {
    calls.push({ url: new URL(route.request().url()).pathname, body: route.request().postDataJSON() })
    if (options.verdict) return options.verdict(route)
    return route.fulfill({ status: 204 })
  })
  await page.goto("/admin")
  return calls
}

test("the just-played Sunday opens with its tally and three groups", async ({ page }) => {
  await open(page)

  await expect(page.getByRole("heading", { name: "Club desk" })).toBeVisible()
  await expect(page.getByRole("button", { name: "Sun Nov 29" })).toHaveAttribute("aria-pressed", "true")
  await expect(page.getByText("$45")).toBeVisible()
  const toCheck = page.getByRole("region", { name: "To check" })
  await expect(toCheck.getByText("Rahim Uddin")).toBeVisible()
  await expect(toCheck.getByText("ZL-88412")).toBeVisible()
  await expect(page.getByRole("region", { name: "Sent back" }).getByText("No transfer with that reference.")).toBeVisible()
  await expect(page.getByRole("region", { name: "Said yes, not reported" }).getByText("Unnamed member")).toBeVisible()
  // Only reported payments can be acted on.
  await expect(page.getByRole("button", { name: /^Confirm / })).toHaveCount(1)
})

test("confirm posts for that member and the list refreshes", async ({ page }) => {
  const calls = await open(page)
  const confirm = page.getByRole("button", { name: "Confirm Rahim Uddin's payment" })
  await expect(confirm).toBeVisible()
  // The refetch after the POST gets the list without Rahim.
  await page.route(/\/api\/sessions\/[^/]+\/payments$/, (route) => json(route, 200, rows.slice(1)))

  await confirm.click()

  await expect(page.getByRole("region", { name: "To check" })).toHaveCount(0)
  expect(calls).toEqual([{ url: `/api/sessions/${PLAYED}/payments/user_rahim/confirm`, body: null }])
})

test("send back needs a reason and posts it trimmed", async ({ page }) => {
  const calls = await open(page)
  await page.getByRole("button", { name: "Send back Rahim Uddin's payment" }).click()
  const reason = page.getByLabel(/Why it can't be confirmed/)
  await page.getByRole("button", { name: "Send back", exact: true }).click()
  await expect(page.getByText("Say why, so the member knows what to fix.")).toBeVisible()
  expect(calls).toEqual([])

  await reason.fill("  Amount was $10, not $15. ")
  await page.getByRole("button", { name: "Send back", exact: true }).click()
  await expect.poll(() => calls).toEqual([{ url: `/api/sessions/${PLAYED}/payments/user_rahim/reject`, body: { reason: "Amount was $10, not $15." } }])
})

test("a refused confirm says the list was refreshed", async ({ page }) => {
  await open(page, { verdict: (route) => json(route, 409, { status: 409 }) })
  await page.getByRole("button", { name: "Confirm Rahim Uddin's payment" }).click()
  await expect(page.getByText(/changed meanwhile/)).toBeVisible()
})

test("another session can be picked; an all-settled one says so", async ({ page }) => {
  await open(page)
  await page.getByRole("button", { name: "Sun Dec 6" }).click()
  await expect(page.getByText("All settled. Nothing to check.")).toBeVisible()
})

test("free and cancelled sessions aren't offered", async ({ page }) => {
  await open(page, {
    weeks: {
      "2026-W48": [summary(PLAYED, "2026-11-29"), summary("free", "2026-11-28", { feeCents: 0 })],
      "2026-W49": [summary("off", "2026-12-03", { status: "Cancelled" })],
    },
  })
  await expect(page.getByRole("group", { name: "Session" }).getByRole("button")).toHaveCount(1)
})

test("a member sees the page is for admins and no payments are asked for", async ({ page }) => {
  let asked = false
  page.on("request", (r) => {
    if (r.url().includes("/payments")) asked = true
  })
  await open(page, { role: "member" })
  await expect(page.getByText("This page is for the club's admins.")).toBeVisible()
  expect(asked).toBe(false)
})

test("signed out is told to sign in as an admin", async ({ page }) => {
  await open(page, { meStatus: 401 })
  await expect(page.getByText("Sign in as a club admin to use this page.")).toBeVisible()
})

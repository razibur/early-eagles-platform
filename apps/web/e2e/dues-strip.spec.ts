import { expect, test, type Page, type Route } from "@playwright/test"

// P1-016 — reporting a Zelle payment from the "This Sunday" card. The API is
// stubbed and the clock fixed on Wednesday 25 November 2026, so the card shows
// Sunday the 29th.

const NOW = new Date(2026, 10, 25, 10, 0)
const ID = "5e550000-0000-0000-0000-000000000029"

const session = (over: Record<string, unknown> = {}) => ({
  id: ID,
  date: "2026-11-29",
  startTime: "07:00:00",
  venue: "Eastside Rec Center",
  feeCents: 1500,
  status: "Scheduled",
  rsvpCounts: { yes: 12, no: 2, maybe: 3 },
  myRsvp: "Yes",
  myPayment: null,
  ...over,
})

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, json: body, contentType: status < 400 ? "application/json" : "application/problem+json" })
}

async function open(page: Page, details: Record<string, unknown>, report?: (route: Route) => Promise<void>) {
  const reports: unknown[] = []
  await page.clock.setFixedTime(NOW)
  await page.route((url) => url.pathname === "/api/sessions", (route) => json(route, 200, [session(details)]))
  await page.route(`**/api/sessions/${ID}`, (route) => json(route, 200, session(details)))
  await page.route(`**/api/sessions/${ID}/payment-report`, async (route) => {
    reports.push(route.request().postDataJSON())
    if (report) return report(route)
    return json(route, 200, { paymentId: "p1" })
  })
  await page.goto("/")
  return { reports, dues: page.getByRole("region", { name: "Your dues" }) }
}

test("not paid yet: report the transfer and the card shows it as reported", async ({ page }) => {
  const { reports, dues } = await open(page, {})
  await expect(dues.getByText("Dues · $15")).toBeVisible()
  await expect(dues.getByText("Not paid yet.")).toBeVisible()

  await dues.getByRole("button", { name: "I've paid" }).click()
  await dues.getByLabel("Name on the transfer").fill("  Rahim Uddin ")
  await dues.getByLabel("Zelle reference").fill("ZL-88412")
  // After the POST the card refetches; answer with the reported state.
  await page.route(`**/api/sessions/${ID}`, (route) =>
    json(route, 200, session({ myPayment: { status: "Reported", amountCents: 1500, rejectionReason: null } })),
  )
  await dues.getByRole("button", { name: "Send" }).click()

  await expect(dues.getByText(/Reported, waiting for your admin/)).toBeVisible()
  await expect(dues.getByRole("button", { name: "I've paid" })).toHaveCount(0)
  expect(reports).toEqual([{ senderName: "Rahim Uddin", reference: "ZL-88412" }])
})

test("blank fields are caught before anything is sent", async ({ page }) => {
  const { reports, dues } = await open(page, {})
  await dues.getByRole("button", { name: "I've paid" }).click()
  await dues.getByLabel("Name on the transfer").fill("   ")
  await dues.getByLabel("Zelle reference").fill("ZL-1")
  await dues.getByRole("button", { name: "Send" }).click()

  await expect(dues.getByText(/Fill in both/)).toBeVisible()
  expect(reports).toEqual([])
})

test("a refused report keeps the form open and says why", async ({ page }) => {
  const { dues } = await open(page, {}, (route) => json(route, 409, { title: "Conflict", status: 409 }))
  await dues.getByRole("button", { name: "I've paid" }).click()
  await dues.getByLabel("Name on the transfer").fill("Rahim")
  await dues.getByLabel("Zelle reference").fill("ZL-1")
  await dues.getByRole("button", { name: "Send" }).click()

  await expect(dues.getByText(/can't take a payment report now/)).toBeVisible()
  await expect(dues.getByLabel("Zelle reference")).toHaveValue("ZL-1")
})

test("a confirmed payment just says paid", async ({ page }) => {
  const { dues } = await open(page, { myPayment: { status: "Confirmed", amountCents: 1500, rejectionReason: null } })
  await expect(dues.getByText("Paid. Thank you.")).toBeVisible()
  await expect(dues.getByRole("button")).toHaveCount(0)
})

test("a rejected payment shows the reason and can be reported again", async ({ page }) => {
  const { dues } = await open(page, { myPayment: { status: "Rejected", amountCents: 1500, rejectionReason: "No transfer with that reference." } })
  await expect(dues.getByText("No transfer with that reference.")).toBeVisible()
  await dues.getByRole("button", { name: "Report again" }).click()
  await expect(dues.getByLabel("Zelle reference")).toBeVisible()
})

test("the amount is what was reported, not today's fee", async ({ page }) => {
  const { dues } = await open(page, { feeCents: 2000, myPayment: { status: "Reported", amountCents: 1500, rejectionReason: null } })
  await expect(dues.getByText("Dues · $15")).toBeVisible()
})

test("a free session and a cancelled one have no dues", async ({ page }) => {
  const free = await open(page, { feeCents: 0 })
  await expect(page.getByText("Free")).toBeVisible()
  await expect(free.dues).toHaveCount(0)

  await page.unrouteAll()
  const off = await open(page, { status: "Cancelled" })
  await expect(page.getByText("Called off")).toBeVisible()
  await expect(off.dues).toHaveCount(0)
})

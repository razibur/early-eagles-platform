import { expect, test, type Page } from "@playwright/test"

// P0-071.3 — /account renders what the API says (/whoami, /me), fetched with
// the API client. The API calls are stubbed in the browser so each state can
// be shown; the real round trip is checked by hand against the local API.

type Stub = { status: number; body: unknown }

async function stubApi(page: Page, whoami: Stub, me: Stub) {
  const seen: string[] = []
  await page.route("**/api/whoami", (route) => {
    seen.push("whoami")
    return route.fulfill({ status: whoami.status, json: whoami.body, contentType: whoami.status < 400 ? "application/json" : "application/problem+json" })
  })
  await page.route("**/api/me", (route) => {
    seen.push("me")
    return route.fulfill({ status: me.status, json: me.body, contentType: me.status < 400 ? "application/json" : "application/problem+json" })
  })
  return seen
}

const club = { status: 200, body: { tenantId: "e0000000-0000-0000-0000-000000000001", slug: "early-eagles" } }
const member = (tenantRole: string | null) => ({
  status: 200,
  body: { userId: "user_2abc", sessionId: "sess_1", organization: null, tenantRole },
})

test("a member sees the club, their role and their user id", async ({ page }) => {
  const seen = await stubApi(page, club, member("member"))
  await page.goto("/account")

  await expect(page.getByRole("heading", { name: "You, at the club" })).toBeVisible()
  await expect(page.getByText("early-eagles")).toBeVisible()
  await expect(page.getByText("Member", { exact: true })).toBeVisible()
  await expect(page.getByText("user_2abc")).toBeVisible()
  expect(seen.sort()).toEqual(["me", "whoami"])
})

test("an admin is shown as club admin", async ({ page }) => {
  await stubApi(page, club, member("tenant-admin"))
  await page.goto("/account")

  await expect(page.getByText("Club admin")).toBeVisible()
})

test("signed in but not in this club says so", async ({ page }) => {
  await stubApi(page, club, member(null))
  await page.goto("/account")

  await expect(page.getByText(/Not a member of this club yet/)).toBeVisible()
})

test("signed out and no club at this address", async ({ page }) => {
  await stubApi(
    page,
    { status: 400, body: { title: "Tenant not resolved", status: 400 } },
    { status: 401, body: { title: "Unauthorized", status: 401 } },
  )
  await page.goto("/account")

  await expect(page.getByText("No club at this address.")).toBeVisible()
  await expect(page.getByText("Not signed in.")).toBeVisible()
})

test("a server error doesn't pretend to know", async ({ page }) => {
  await stubApi(page, { status: 503, body: { title: "API not configured", status: 503 } }, { status: 500, body: {} })
  await page.goto("/account")

  await expect(page.getByText("Couldn't reach the club right now.")).toHaveCount(2)
})

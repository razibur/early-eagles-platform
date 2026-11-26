import { expect, test, type Page } from "@playwright/test"

import { ADMIN_HOME_PATH, AFTER_AUTH_PATH, landingFor } from "../lib/auth/routes"
import { isPublicPath } from "../lib/auth/public-routes"

// P1-035 — after sign-in, admins land on /admin and everyone else on /
// (ADR-0004). Sign-in lands on /welcome, which asks /me (stubbed here) for the
// role in this club and replaces itself with the right home.

async function welcome(page: Page, me: { status: number; body: unknown }) {
  await page.route("**/api/me", (route) =>
    route.fulfill({ status: me.status, json: me.body, contentType: me.status < 400 ? "application/json" : "application/problem+json" }),
  )
  // The homes call the API too; keep them quiet.
  await page.route((url) => url.pathname.startsWith("/api/sessions"), (route) => route.fulfill({ status: 200, json: [] }))
  await page.goto(AFTER_AUTH_PATH)
}

const role = (tenantRole: string | null) => ({
  status: 200,
  body: { userId: "user_1", sessionId: "s", organization: null, tenantRole },
})

test("an admin lands on the club desk", async ({ page }) => {
  await welcome(page, role("tenant-admin"))
  await expect(page).toHaveURL(/\/admin$/)
  await expect(page.getByRole("heading", { name: "Club desk" })).toBeVisible()
})

test("a member lands on the member home", async ({ page }) => {
  await welcome(page, role("member"))
  await expect(page).toHaveURL(/\/$/)
})

test("someone who isn't in this club lands on the member home", async ({ page }) => {
  await welcome(page, role(null))
  await expect(page).toHaveURL(/\/$/)
})

test("an API error still moves on, to the member home", async ({ page }) => {
  await welcome(page, { status: 500, body: { status: 500 } })
  await expect(page).toHaveURL(/\/$/)
})

test("the welcome stop is replaced, so Back doesn't return to it", async ({ page }) => {
  await page.goto("/settings")
  await welcome(page, role("tenant-admin"))
  await expect(page).toHaveURL(/\/admin$/)
  await page.goBack()
  await expect(page).toHaveURL(/\/settings$/)
})

test.describe("landing rules", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  test("only the admin role goes to /admin", () => {
    expect(landingFor("tenant-admin")).toBe(ADMIN_HOME_PATH)
    expect(landingFor("member")).toBe("/")
    expect(landingFor(null)).toBe("/")
    expect(landingFor(undefined)).toBe("/")
    expect(landingFor("org:admin")).toBe("/")
  })

  test("the welcome stop needs a signed-in user", () => {
    expect(isPublicPath(AFTER_AUTH_PATH)).toBe(false)
  })
})

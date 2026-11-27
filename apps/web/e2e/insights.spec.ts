import { expect, test, type Page } from "@playwright/test"

import { INSIGHTS_PATH } from "../lib/auth/routes"
import { isPublicPath } from "../lib/auth/public-routes"

// P1-034 — the sample chart dashboard moved from / to /insights, for admins
// (ADR-0004). /me is stubbed; the charts read no API yet.

async function stubApi(page: Page, me: { status: number; tenantRole?: string | null }) {
  await page.route("**/api/me", (route) =>
    route.fulfill({
      status: me.status,
      json: me.status < 400 ? { userId: "user_1", sessionId: "s", organization: null, tenantRole: me.tenantRole ?? null } : { status: me.status },
      contentType: me.status < 400 ? "application/json" : "application/problem+json",
    }),
  )
  // The home's "This Sunday" card asks for sessions; keep it quiet.
  await page.route((url) => url.pathname.startsWith("/api/sessions"), (route) => route.fulfill({ status: 200, json: [] }))
}

test("an admin sees the charts, marked as sample figures", async ({ page }) => {
  await stubApi(page, { status: 200, tenantRole: "tenant-admin" })
  await page.goto(INSIGHTS_PATH)

  await expect(page.getByRole("heading", { level: 1, name: "Insights" })).toBeVisible()
  await expect(page.getByText("Sample figures")).toBeVisible()
  await expect(page.getByRole("heading", { name: "Twelve Sundays, by the head-count." })).toBeVisible()
  await expect(page.getByRole("heading", { name: "Three rivers, one tide." })).toBeVisible()
  await expect(page.getByRole("heading", { name: "Slow growth is still growth." })).toBeVisible()
  await expect(page.locator(".recharts-responsive-container").first()).toBeVisible()
  await expect(page).toHaveTitle("Insights — Early Eagles")
  // The charts fit the screen at every width.
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  expect(overflow, "horizontal overflow in px").toBeLessThanOrEqual(0)
})

test("a member is told the page is for admins and sees no charts", async ({ page }) => {
  await stubApi(page, { status: 200, tenantRole: "member" })
  await page.goto(INSIGHTS_PATH)

  await expect(page.getByText("This page is for the club's admins.")).toBeVisible()
  await expect(page.getByRole("heading", { name: "Twelve Sundays, by the head-count." })).toHaveCount(0)
  await expect(page.locator(".recharts-responsive-container")).toHaveCount(0)
})

test("a signed-out answer asks to sign in as an admin", async ({ page }) => {
  await stubApi(page, { status: 401 })
  await page.goto(INSIGHTS_PATH)

  await expect(page.getByText("Sign in as a club admin to use this page.")).toBeVisible()
  await expect(page.locator(".recharts-responsive-container")).toHaveCount(0)
})

test("the member home no longer carries the charts", async ({ page }) => {
  await stubApi(page, { status: 200, tenantRole: "tenant-admin" })
  await page.goto("/")

  await expect(page.getByRole("heading", { level: 1, name: "Early Eagles" })).toBeVisible()
  await expect(page.getByText("No session on the board")).toBeVisible()
  await expect(page.getByRole("heading", { name: "Twelve Sundays, by the head-count." })).toHaveCount(0)
  await expect(page.locator(".recharts-responsive-container")).toHaveCount(0)
})

test("insights needs a signed-in user", ({}, testInfo) => {
  test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  expect(isPublicPath(INSIGHTS_PATH)).toBe(false)
})

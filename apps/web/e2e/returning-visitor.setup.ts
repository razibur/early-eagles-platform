import { expect, test as setup } from "@playwright/test"

// P0-070.5 — the state of a member on their second visit, for the iphone
// project: the cookie banner answered and the install card dismissed. On an
// iPhone screen those two cards cover the lower half of the page, so specs
// that aren't about them start from here. cookie-consent and install-card
// specs opt out and start fresh.

// Keep in step with RETURNING_VISITOR_STATE in playwright.config.ts.
const RETURNING_VISITOR_STATE = "e2e/.state/returning-iphone.json"

setup("returning visitor on an iPhone", async ({ page }) => {
  await page.goto("/")
  await page.getByRole("button", { name: "Necessary only" }).click()
  // Safari can't prompt, so the install card explains the steps instead.
  await page.getByRole("button", { name: "Got it" }).click()
  await expect(page.getByRole("dialog")).toHaveCount(0)
  await page.context().storageState({ path: RETURNING_VISITOR_STATE })
})

import { expect, test } from "@playwright/test"

// P0-039.3 — the browser tests run with EE_AUTH_DISABLED=1 (no Clerk
// instance), where there is nothing to sign in with: the sign-in and sign-up
// pages, and Clerk's sub-steps under them, are 404s rather than broken forms.
// With real keys they render Clerk's card; that is checked by hand against
// the development instance (walkthrough §4.11).
for (const path of ["/sign-in", "/sign-up", "/sign-in/factor-one", "/sign-up/verify-email-address"]) {
  test(`${path} is not found when sign-in is off`, async ({ page }) => {
    const response = await page.goto(path)
    expect(response?.status()).toBe(404)
  })
}

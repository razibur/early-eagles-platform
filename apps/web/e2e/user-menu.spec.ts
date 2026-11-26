import { expect, test } from "@playwright/test"

// P0-039.5 — the account menu in a build without Clerk (EE_AUTH_DISABLED=1):
// a guest, sign-in shown as unavailable rather than linking to a 404, and no
// sign-out. The signed-in state (name, avatar, sign out) comes from Clerk and
// is checked by hand against the development instance.
test("without Clerk the account menu is a guest with no way to sign in or out", async ({ page }) => {
  await page.goto("/settings")
  await page.getByRole("button", { name: "Account menu" }).click()

  const menu = page.getByRole("menu")
  await expect(menu.getByText("Guest")).toBeVisible()
  await expect(menu.getByText("Not signed in")).toBeVisible()
  await expect(menu.getByRole("menuitem", { name: /Sign in/ })).toBeDisabled()
  await expect(menu.getByText("Not available in this build")).toBeVisible()
  await expect(menu.getByRole("menuitem", { name: "Settings" })).toBeVisible()
  await expect(menu.getByRole("menuitem", { name: "Sign out" })).toHaveCount(0)
})

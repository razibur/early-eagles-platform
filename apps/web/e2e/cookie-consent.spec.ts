import { expect, test } from "@playwright/test"

// P0-076.1 / P0-076.2 — the consent banner and the policy behind it.

const banner = (page: import("@playwright/test").Page) => page.getByRole("dialog", { name: /cookies, briefly|cookie settings/i })

async function consentCookie(page: import("@playwright/test").Page) {
  const cookie = (await page.context().cookies()).find((c) => c.name === "ee_consent")
  return cookie ? JSON.parse(decodeURIComponent(cookie.value)).state : null
}

test("first visit shows the banner with equal-weight choices", async ({ page }) => {
  await page.goto("/")
  await expect(banner(page)).toBeVisible()
  await expect(page.getByRole("button", { name: "Accept analytics" })).toBeVisible()
  await expect(page.getByRole("button", { name: "Necessary only" })).toBeVisible()
  expect(await consentCookie(page)).toBeNull()
})

test("necessary only is remembered and the banner stays away", async ({ page }) => {
  await page.goto("/")
  await page.getByRole("button", { name: "Necessary only" }).click()
  await expect(banner(page)).toBeHidden()
  expect(await consentCookie(page)).toMatchObject({ analytics: false })

  await page.reload()
  await page.waitForLoadState("networkidle")
  await expect(banner(page)).toBeHidden()
})

test("cookie settings in the footer reopens preferences and saves a change", async ({ page }) => {
  await page.goto("/")
  await page.getByRole("button", { name: "Accept analytics" }).click()
  expect(await consentCookie(page)).toMatchObject({ analytics: true })

  await page.getByRole("button", { name: "Cookie settings" }).click()
  await expect(page.getByRole("dialog", { name: "Cookie settings" })).toBeVisible()
  const analyticsSwitch = page.getByRole("switch")
  await expect(analyticsSwitch).toHaveAttribute("aria-checked", "true")
  await analyticsSwitch.click()
  await page.getByRole("button", { name: "Save choices" }).click()

  await expect(banner(page)).toBeHidden()
  expect(await consentCookie(page)).toMatchObject({ analytics: false })
})

test("the policy page is linked from the banner and lists what is stored", async ({ page }) => {
  await page.goto("/")
  await banner(page).getByRole("link", { name: "Cookie policy" }).click()
  await expect(page).toHaveURL(/\/cookies$/)
  await expect(page.getByRole("heading", { name: "Cookies", exact: true })).toBeVisible()
  await expect(page.getByRole("cell", { name: "ee_consent" })).toBeVisible()
  await expect(page.getByRole("cell", { name: "ee-theme" })).toBeVisible()
})

import { expect, test } from "@playwright/test"

import { navItems } from "../lib/navigation"

// P0-070.4 — the app shell at phone, tablet and desktop widths (the three
// projects in playwright.config.ts). Structural checks rather than pixel
// snapshots: pixel baselines differ between Windows and the Linux CI runner
// and turn into noise. A full-page screenshot is attached to every test for
// eyeballing in the HTML report.

const phone = (width: number) => width < 768

for (const { href, label } of navItems) {
  test(`${label} (${href}) lays out without overflow`, async ({ page }, testInfo) => {
    const errors: string[] = []
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(message.text())
    })
    page.on("pageerror", (error) => errors.push(error.message))

    const response = await page.goto(href)
    expect(response?.status()).toBe(200)

    // Nothing pushes the page wider than the screen.
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
    expect(overflow, "horizontal overflow in px").toBeLessThanOrEqual(0)

    // The top bar is always there, with the account menu.
    await expect(page.getByRole("link", { name: "Early Eagles — home" })).toBeVisible()
    await expect(page.getByRole("button", { name: "Account menu" })).toBeVisible()
    await expect(page.getByRole("button", { name: "Theme" })).toBeVisible()

    // Exactly one navigation is visible, and it marks this section.
    const width = page.viewportSize()!.width
    const bottomNav = page.locator("nav.fixed")
    const desktopNav = page.locator("header nav")
    if (phone(width)) {
      await expect(bottomNav).toBeVisible()
      await expect(desktopNav).toBeHidden()
      await expect(bottomNav.locator('[aria-current="page"]')).toHaveText(label)

      // The bar must not cover the end of the page: the body pads by its height.
      const covered = await page.evaluate(() => {
        const bar = document.querySelector("nav.fixed")!.getBoundingClientRect().height
        return parseFloat(getComputedStyle(document.body).paddingBottom) >= bar
      })
      expect(covered, "body bottom padding clears the bottom nav").toBe(true)
    } else {
      await expect(bottomNav).toBeHidden()
      await expect(desktopNav).toBeVisible()
      if (href !== "/settings") {
        await expect(desktopNav.locator('[aria-current="page"]')).toHaveText(label)
      }
    }

    await testInfo.attach(`${testInfo.project.name}${href.replace("/", "-") || "-home"}`, {
      body: await page.screenshot({ fullPage: true }),
      contentType: "image/png",
    })
    expect(errors).toEqual([])
  })
}

test("theme choice survives a reload at this width", async ({ page }) => {
  await page.goto("/")
  await page.getByRole("button", { name: "Theme" }).click()
  await page.getByRole("menuitemradio", { name: "Light" }).click()
  await expect(page.locator("html")).toHaveClass(/\blight\b/)

  await page.reload()
  await expect(page.locator("html")).toHaveClass(/\blight\b/)
})

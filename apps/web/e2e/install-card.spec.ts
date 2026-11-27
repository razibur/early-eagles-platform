import { expect, test, type Page } from "@playwright/test"

// P1-023 — the install card. Chrome only fires a real beforeinstallprompt on its
// own schedule, so these tests fire one by hand and watch what the page does with it.

// About the first-visit card: start fresh even in the iphone project, which
// otherwise starts as a returning visitor (e2e/returning-visitor.setup.ts).
test.use({ storageState: { cookies: [], origins: [] } })

const iPhone =
  "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1"

const card = (page: Page) => page.getByRole("dialog", { name: "Keep the club a tap away" })

async function decideCookies(page: Page) {
  await page.getByRole("button", { name: "Necessary only" }).click()
}

async function offerInstall(page: Page) {
  return page.evaluate(() => {
    const event = new Event("beforeinstallprompt", { cancelable: true })
    Object.assign(event, {
      prompt: () => {
        Object.assign(window, { installPrompted: true })
        return Promise.resolve()
      },
    })
    window.dispatchEvent(event)
    return event.defaultPrevented
  })
}

async function open(page: Page) {
  await page.goto("/")
  await expect(page.getByRole("button", { name: "Necessary only" })).toBeVisible()
}

async function reopen(page: Page) {
  await page.reload()
  await page.waitForLoadState("networkidle")
}

test.describe("with a browser that can install", () => {
  test("waits for the cookie decision, then replaces Chrome's own bar", async ({ page }) => {
    await open(page)

    expect(await offerInstall(page)).toBe(true)
    await expect(card(page)).toBeHidden()

    await decideCookies(page)
    await expect(card(page)).toBeVisible()
    await expect(page.getByRole("dialog")).toHaveCount(1)
  })

  test("reopening cookie settings gives the spot back", async ({ page }) => {
    await open(page)
    await offerInstall(page)
    await decideCookies(page)
    await expect(card(page)).toBeVisible()

    // On a phone the card floats over the footer link, so this click can't be a real tap.
    await page.getByRole("button", { name: "Cookie settings" }).dispatchEvent("click")
    await expect(card(page)).toBeHidden()

    await page.getByRole("button", { name: "Cancel" }).click()
    await expect(card(page)).toBeVisible()
  })

  test("install runs the browser's prompt and closes the card", async ({ page }) => {
    await open(page)
    await offerInstall(page)
    await decideCookies(page)

    await card(page).getByRole("button", { name: "Install" }).click()

    await expect(card(page)).toBeHidden()
    expect(await page.evaluate(() => "installPrompted" in window)).toBe(true)
  })

  test("an install from the browser menu closes the card", async ({ page }) => {
    await open(page)
    await offerInstall(page)
    await decideCookies(page)
    await expect(card(page)).toBeVisible()

    await page.evaluate(() => window.dispatchEvent(new Event("appinstalled")))

    await expect(card(page)).toBeHidden()
  })

  for (const [how, answer] of [
    ["declined", (page: Page) => card(page).getByRole("button", { name: "Not now" }).click()],
    ["ignored", async () => {}],
  ] as const) {
    test(`once ${how}, it doesn't come back on this device`, async ({ page }) => {
      await open(page)
      await offerInstall(page)
      await decideCookies(page)
      await expect(card(page)).toBeVisible()
      await answer(page)

      await reopen(page)
      expect(await offerInstall(page)).toBe(true)

      await expect(card(page)).toBeHidden()
    })
  }
})

test("a browser with nothing to offer sees no card", async ({ page }) => {
  await open(page)
  await decideCookies(page)
  await reopen(page)

  await expect(card(page)).toBeHidden()
})

test.describe("on an iPhone", () => {
  test.use({ userAgent: iPhone })

  test("explains the Share menu, once", async ({ page }) => {
    await open(page)
    await decideCookies(page)

    await expect(card(page).getByText("Add to Home Screen")).toBeVisible()
    await expect(card(page).getByRole("button", { name: "Install" })).toBeHidden()

    await card(page).getByRole("button", { name: "Got it" }).click()
    await expect(card(page)).toBeHidden()

    await reopen(page)
    await expect(card(page)).toBeHidden()
  })

  test("says nothing once it's on the home screen", async ({ page }) => {
    await page.addInitScript(() => Object.defineProperty(navigator, "standalone", { value: true }))
    await open(page)
    await decideCookies(page)
    await reopen(page)

    await expect(card(page)).toBeHidden()
    expect(await page.evaluate(() => localStorage.getItem("ee-install-card"))).toBeNull()
  })
})

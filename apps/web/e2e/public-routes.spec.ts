import { expect, test } from "@playwright/test"

import { isPublicPath } from "../lib/auth/public-routes"

// P0-039.4 — which pages a signed-out visitor may open; the proxy sends them
// to /sign-in for everything else. Pure function tests: run once.
test.describe("isPublicPath", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  const open = ["/sign-in", "/sign-in/factor-one", "/sign-in/sso-callback", "/sign-up", "/sign-up/verify-email-address", "/cookies", "/SIGN-IN"]
  for (const path of open) {
    test(`${path} is public`, () => expect(isPublicPath(path)).toBe(true))
  }

  const closed = ["/", "/roster", "/wall", "/events", "/chat", "/settings", "/sign-in-please", "/sign-upgrade", "/cookies-and-cream", "/api/sign-in", "//sign-in"]
  for (const path of closed) {
    test(`${path} needs sign-in`, () => expect(isPublicPath(path)).toBe(false))
  }
})

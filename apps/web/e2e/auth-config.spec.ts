import { expect, test } from "@playwright/test"

import { authMode } from "../lib/auth/config"

// P0-039.1 — sign-in fails closed: no publishable key means no pages, unless
// the build says explicitly that it runs without Clerk. Pure function tests:
// no browser needed, so they run once rather than per viewport.
test.describe("authMode", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  // Shape only; not a real instance. Built at runtime so the secret scanner
  // doesn't read a key-shaped literal (a publishable key is base64 of the host).
  const fakeKey = (kind: "test" | "live", host: string) => `pk_${kind}_${btoa(`${host}$`)}`
  const testKey = fakeKey("test", "example.clerk.accounts.dev")

  test("a test or live publishable key turns Clerk on", () => {
    expect(authMode({ NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: testKey })).toBe("clerk")
    expect(authMode({ NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: fakeKey("live", "clerk.example.com") })).toBe("clerk")
  })

  test("EE_AUTH_DISABLED=1 turns it off, key or not", () => {
    expect(authMode({ EE_AUTH_DISABLED: "1" })).toBe("disabled")
    expect(authMode({ EE_AUTH_DISABLED: "1", NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: testKey })).toBe("disabled")
  })

  for (const value of [undefined, "", "   "]) {
    test(`a missing key (${JSON.stringify(value)}) refuses to run`, () => {
      expect(() => authMode({ NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: value })).toThrow(/EE_AUTH_DISABLED=1/)
    })
  }

  for (const disabled of ["0", "true", "yes", ""]) {
    test(`EE_AUTH_DISABLED=${JSON.stringify(disabled)} is not an opt-out`, () => {
      expect(() => authMode({ EE_AUTH_DISABLED: disabled })).toThrow()
    })
  }

  for (const key of ["sk_test_abc", "pk_test_", "pk_prod_abc", "pk_test_has space"]) {
    test(`${JSON.stringify(key)} is not a publishable key`, () => {
      expect(() => authMode({ NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: key })).toThrow(/publishable key/)
    })
  }
})

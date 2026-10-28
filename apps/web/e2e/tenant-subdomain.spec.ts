import { expect, test } from "@playwright/test"

import { SUPPORTED_BASE_DOMAINS, extractTenantSlug } from "../lib/tenancy/subdomain"

// P0-077.1 — the web app's slug extraction. Every case below is copied from
// the API's SubdomainExtractorTests, so the two can't silently disagree about
// which tenant a host belongs to. Pure function tests: no browser needed, so
// they run once rather than per viewport.
test.describe("extractTenantSlug matches the API's SubdomainExtractor", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  const resolves: [string, string][] = [
    ["early-eagles.earlyeagles.club", "early-eagles"],
    ["eagles.earlyeagles.club", "eagles"],
    ["a1b2c3.earlyeagles.club", "a1b2c3"],
    ["early-eagles.localhost", "early-eagles"],
    ["eagles.localhost", "eagles"],
    ["early-eagles.localhost:3000", "early-eagles"],
    ["early-eagles.earlyeagles.club:443", "early-eagles"],
    ["EARLY-EAGLES.EARLYEAGLES.CLUB", "early-eagles"],
  ]
  for (const [host, slug] of resolves) {
    test(`${host} → ${slug}`, () => {
      expect(extractTenantSlug(host)).toBe(slug)
    })
  }

  const rejects: (string | null)[] = [
    null,
    "",
    "   ",
    "earlyeagles.club",
    "localhost",
    "localhost:3000",
    "slug.example.com",
    "eagles.notearlyeagles.club",
    "foo.bar",
    "a.b.earlyeagles.club",
    "api.staging.earlyeagles.club",
    "foo.bar.localhost",
    ".earlyeagles.club",
    ".localhost",
    "127.0.0.1",
    "127.0.0.1:3000",
    "192.168.1.1",
    "[::1]",
    "[::1]:3000",
    "[2001:db8::1]:443",
  ]
  for (const host of rejects) {
    test(`${JSON.stringify(host)} → no tenant`, () => {
      expect(extractTenantSlug(host)).toBeNull()
    })
  }

  test("supports both prod and dev base domains", () => {
    expect(SUPPORTED_BASE_DOMAINS).toContain("earlyeagles.club")
    expect(SUPPORTED_BASE_DOMAINS).toContain("localhost")
  })
})

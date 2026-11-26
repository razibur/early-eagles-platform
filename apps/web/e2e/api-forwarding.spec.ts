import { expect, test } from "@playwright/test"

import { apiForwarding, apiTarget, forwardedHeaders, isApiPath } from "../lib/api/forwarding"
import { E2E_PROXY_KEY } from "./fixtures/echo-api-settings"

// P0-071.5 / ADR-0016 — the browser calls /api/* on its own address and the
// web app forwards it to the API with the club host and the proxy key. Here
// the API is e2e/fixtures/echo-api.mjs, which answers with what it received.
// Request tests: no page rendering, so they run once rather than per viewport.
test.describe("/api forwarding", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "no page involved; run once")
  })

  type Echo = { method: string; path: string; headers: Record<string, string>; body: string }

  test("forwards path, query, method, body and the browser's Authorization", async ({ request }) => {
    const response = await request.post("/api/sessions/42/rsvp?source=test", {
      headers: { Authorization: "Bearer fake.session.token" },
      data: { answer: "yes" },
    })

    expect(response.status()).toBe(200)
    const echo = (await response.json()) as Echo
    expect(echo.method).toBe("POST")
    expect(echo.path).toBe("/sessions/42/rsvp?source=test")
    expect(echo.headers.authorization).toBe("Bearer fake.session.token")
    expect(JSON.parse(echo.body)).toEqual({ answer: "yes" })
  })

  test("tells the API which club address was called, with the key", async ({ request }) => {
    const response = await request.get("/api/whoami", { headers: { Host: "early-eagles.localhost:3200" } })

    const echo = (await response.json()) as Echo
    expect(echo.path).toBe("/whoami")
    expect(echo.headers["x-forwarded-host"]).toBe("early-eagles.localhost:3200")
    expect(echo.headers["x-ee-proxy-key"]).toBe(E2E_PROXY_KEY)
  })

  test("the browser can't choose the key or the forwarded host", async ({ request }) => {
    const response = await request.get("/api/whoami", {
      headers: {
        Host: "early-eagles.localhost:3200",
        "X-EE-Proxy-Key": "guessed-key",
        "X-Forwarded-Host": "other-club.localhost:3200",
      },
    })

    const echo = (await response.json()) as Echo
    expect(echo.headers["x-ee-proxy-key"]).toBe(E2E_PROXY_KEY)
    expect(echo.headers["x-forwarded-host"]).toBe("early-eagles.localhost:3200")
  })

  test("/api itself maps to the API root; /apiary is a page, not the API", async ({ request }) => {
    const root = (await (await request.get("/api")).json()) as Echo
    expect(root.path).toBe("/")

    const page = await request.get("/apiary")
    expect(page.headers()["content-type"]).toContain("text/html")
  })
})

test.describe("forwarding helpers", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  test("isApiPath matches /api on a segment boundary", () => {
    expect(isApiPath("/api")).toBe(true)
    expect(isApiPath("/api/me")).toBe(true)
    expect(isApiPath("/apiary")).toBe(false)
    expect(isApiPath("/settings/api")).toBe(false)
  })

  test("apiTarget strips the prefix and keeps the query", () => {
    const origin = new URL("https://ee-api.example.io")
    expect(apiTarget("/api/me", "", origin).href).toBe("https://ee-api.example.io/me")
    expect(apiTarget("/api", "?a=1", origin).href).toBe("https://ee-api.example.io/?a=1")
  })

  test("apiForwarding needs both settings and a bare origin", () => {
    expect(apiForwarding({})).toBeNull()
    expect(apiForwarding({ EE_API_ORIGIN: "http://localhost:5174" })).toBeNull()
    expect(apiForwarding({ EE_API_PROXY_KEY: "k" })).toBeNull()
    expect(apiForwarding({ EE_API_ORIGIN: "http://localhost:5174", EE_API_PROXY_KEY: "k" })?.origin.href).toBe("http://localhost:5174/")
    for (const bad of ["localhost:5174", "ftp://api.example.io", "https://api.example.io/v1", "https://api.example.io/?x=1"]) {
      expect(() => apiForwarding({ EE_API_ORIGIN: bad, EE_API_PROXY_KEY: "k" })).toThrow(/EE_API_ORIGIN/)
    }
  })

  test("forwardedHeaders replaces whatever the browser sent", () => {
    const incoming = new Headers({ authorization: "Bearer t", "x-ee-proxy-key": "guess", "x-forwarded-host": "evil" })
    const out = forwardedHeaders(incoming, "alpha.earlyeagles.club", "real-key")
    expect(out.get("authorization")).toBe("Bearer t")
    expect(out.get("x-ee-proxy-key")).toBe("real-key")
    expect(out.get("x-forwarded-host")).toBe("alpha.earlyeagles.club")
  })
})

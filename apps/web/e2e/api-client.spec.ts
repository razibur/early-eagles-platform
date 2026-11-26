import { expect, test } from "@playwright/test"

import { ApiError, createApiFetch } from "../lib/api/client"

// P0-071.2 — the browser's API client: same-origin /api/<path>, the Clerk
// session token as a bearer, problem details as a typed error. Pure tests with
// a recording fake fetch; run once.
test.describe("createApiFetch", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== "desktop", "pure function; run once")
  })

  type Call = { url: string; init: RequestInit }

  function recorder(response: () => Response) {
    const calls: Call[] = []
    const fetchImpl = (async (url: string, init: RequestInit) => {
      calls.push({ url, init })
      return response()
    }) as unknown as typeof fetch
    return { calls, fetchImpl }
  }

  const ok = (body: unknown) => () => Response.json(body)

  test("calls /api<path> with the session token as a bearer", async () => {
    const { calls, fetchImpl } = recorder(ok({ userId: "user_1" }))
    const apiFetch = createApiFetch(async () => "session.jwt", fetchImpl)

    const me = await apiFetch<{ userId: string }>("/me")

    expect(me.userId).toBe("user_1")
    expect(calls).toHaveLength(1)
    expect(calls[0].url).toBe("/api/me")
    const headers = new Headers(calls[0].init.headers)
    expect(headers.get("authorization")).toBe("Bearer session.jwt")
    expect(headers.get("accept")).toContain("application/json")
  })

  test("signed out (no token) sends no Authorization header at all", async () => {
    const { calls, fetchImpl } = recorder(ok({}))
    await createApiFetch(async () => null, fetchImpl)("/whoami")

    expect(new Headers(calls[0].init.headers).has("authorization")).toBe(false)
  })

  test("asks for the token on every call, so a refreshed token is used", async () => {
    const tokens = ["first", "second"]
    const { calls, fetchImpl } = recorder(ok({}))
    const apiFetch = createApiFetch(async () => tokens.shift() ?? null, fetchImpl)

    await apiFetch("/me")
    await apiFetch("/me")

    expect(calls.map((c) => new Headers(c.init.headers).get("authorization"))).toEqual(["Bearer first", "Bearer second"])
  })

  test("a json body is serialised with its content type; other headers survive", async () => {
    const { calls, fetchImpl } = recorder(ok({ id: 7 }))
    await createApiFetch(async () => "t", fetchImpl)("/sessions/1/rsvp", {
      method: "PUT",
      json: { answer: "yes" },
      headers: { "x-request-id": "abc" },
    })

    const { init } = calls[0]
    const headers = new Headers(init.headers)
    expect(init.method).toBe("PUT")
    expect(init.body).toBe('{"answer":"yes"}')
    expect(headers.get("content-type")).toBe("application/json")
    expect(headers.get("x-request-id")).toBe("abc")
  })

  test("a caller can't override the bearer with its own Authorization header", async () => {
    const { calls, fetchImpl } = recorder(ok({}))
    await createApiFetch(async () => "real", fetchImpl)("/me", { headers: { Authorization: "Bearer forged" } })

    expect(new Headers(calls[0].init.headers).get("authorization")).toBe("Bearer real")
  })

  test("204 No Content resolves to undefined", async () => {
    const { fetchImpl } = recorder(() => new Response(null, { status: 204 }))
    expect(await createApiFetch(async () => "t", fetchImpl)("/push/subscriptions", { method: "DELETE" })).toBeUndefined()
  })

  test("a problem response throws ApiError carrying the problem details", async () => {
    const problem = { title: "Forbidden", status: 403, detail: "Admins only." }
    const { fetchImpl } = recorder(
      () => new Response(JSON.stringify(problem), { status: 403, headers: { "content-type": "application/problem+json" } }),
    )

    const error = await createApiFetch(async () => "t", fetchImpl)("/audit-events").catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(403)
    expect((error as ApiError).problem).toEqual(problem)
    expect((error as ApiError).message).toBe("403 Forbidden")
  })

  test("a non-JSON error still throws ApiError, with no problem", async () => {
    const { fetchImpl } = recorder(() => new Response("Bad gateway", { status: 502, headers: { "content-type": "text/plain" } }))

    const error = (await createApiFetch(async () => "t", fetchImpl)("/me").catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.problem).toBeNull()
  })

  for (const path of ["me", "//evil.example.com/me"]) {
    test(`refuses the path ${JSON.stringify(path)}`, async () => {
      const { calls, fetchImpl } = recorder(ok({}))
      const apiFetch = createApiFetch(async () => "t", fetchImpl)

      await expect(apiFetch(path as `/${string}`)).rejects.toThrow(/one '\/'/)
      expect(calls).toHaveLength(0)
    })
  }
})

import { API_PREFIX } from "./forwarding"

// The browser's API client (P0-071.2). Every call goes to /api/<path> on the
// page's own origin, which proxy.ts forwards to the API (ADR-0016), and
// carries the Clerk session token when there is one. Kept free of React and
// Clerk so it can be tested with a fake fetch; the provider wires in Clerk's
// getToken.

/** Returns the current session token, or null when signed out / no Clerk. */
export type GetToken = () => Promise<string | null>

/** RFC 7807 problem details, as the API's errors are shaped. */
export type ProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
  [extension: string]: unknown
}

/** A non-2xx answer from the API. `problem` is set when the body was problem JSON. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.title ? `${status} ${problem.title}` : `API request failed with ${status}`)
    this.name = "ApiError"
  }
}

export type ApiRequest = Omit<RequestInit, "body"> & {
  /** Sent as a JSON body with the matching content type. */
  json?: unknown
}

export type ApiFetch = <T>(path: `/${string}`, request?: ApiRequest) => Promise<T>

export function createApiFetch(getToken: GetToken, fetchImpl: typeof fetch = (...args) => fetch(...args)): ApiFetch {
  return async function apiFetch<T>(path: `/${string}`, request: ApiRequest = {}): Promise<T> {
    if (!path.startsWith("/") || path.startsWith("//")) throw new Error(`API paths start with one '/': ${path}`)

    const { json, headers: given, ...init } = request
    const headers = new Headers(given)
    headers.set("accept", "application/json, application/problem+json")

    const token = await getToken()
    if (token) headers.set("authorization", `Bearer ${token}`)

    let body: BodyInit | undefined
    if (json !== undefined) {
      headers.set("content-type", "application/json")
      body = JSON.stringify(json)
    }

    const response = await fetchImpl(`${API_PREFIX}${path}`, { ...init, headers, body })
    if (!response.ok) throw new ApiError(response.status, await readProblem(response))
    if (response.status === 204 || response.headers.get("content-length") === "0") return undefined as T
    return (await response.json()) as T
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const type = response.headers.get("content-type") ?? ""
  if (!type.includes("json")) return null
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

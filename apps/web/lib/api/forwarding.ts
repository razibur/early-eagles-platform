// Same-origin API forwarding (P0-071.5, ADR-0016). The browser calls
// /api/<path> on the club's own address; proxy.ts forwards it to
// <EE_API_ORIGIN>/<path>, telling the API which address was used
// (X-Forwarded-Host) and proving it is the web app (X-EE-Proxy-Key). The API
// believes the forwarded host only with the key, and the key never comes from
// the browser: any copy it sends is dropped here.
//
// Server-only: the key is a secret and has no NEXT_PUBLIC_ prefix.

export const API_PREFIX = "/api"
export const PROXY_KEY_HEADER = "x-ee-proxy-key"
export const FORWARDED_HOST_HEADER = "x-forwarded-host"

export type ApiForwarding = { origin: URL; key: string }

/** True for /api and anything under it, on a segment boundary. */
export function isApiPath(pathname: string): boolean {
  return pathname === API_PREFIX || pathname.startsWith(`${API_PREFIX}/`)
}

/**
 * The forwarding settings, or null when they're missing (then /api/* answers
 * 503). EE_API_ORIGIN is the API's base URL, e.g. http://localhost:5174;
 * EE_API_PROXY_KEY equals the API's TrustedProxy:Key.
 */
export function apiForwarding(env: Record<string, string | undefined> = process.env): ApiForwarding | null {
  const origin = env.EE_API_ORIGIN?.trim()
  const key = env.EE_API_PROXY_KEY?.trim()
  if (!origin || !key) return null

  let url: URL
  try {
    url = new URL(origin)
  } catch {
    throw new Error(`EE_API_ORIGIN is not a URL: ${origin}`)
  }
  if ((url.protocol !== "http:" && url.protocol !== "https:") || url.pathname !== "/" || url.search || url.hash) {
    throw new Error(`EE_API_ORIGIN must be a bare origin like https://api.example.com, not ${origin}`)
  }
  return { origin: url, key }
}

/** /api/me?x=1 → <origin>/me?x=1. */
export function apiTarget(pathname: string, search: string, origin: URL): URL {
  const rest = pathname.slice(API_PREFIX.length) || "/"
  return new URL(`${rest}${search}`, origin)
}

/** The request headers sent on: the browser's own, minus anything it may not choose. */
export function forwardedHeaders(incoming: Headers, host: string, key: string): Headers {
  const headers = new Headers(incoming)
  headers.delete(PROXY_KEY_HEADER)
  headers.set(FORWARDED_HOST_HEADER, host)
  headers.set(PROXY_KEY_HEADER, key)
  return headers
}

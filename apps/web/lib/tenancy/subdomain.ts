// Tenant slug from the request host (P0-077.1). Must agree exactly with the
// API's SubdomainExtractor (apps/api/src/EarlyEagles.Infrastructure/Tenancy/
// SubdomainExtractor.cs): same base domains, one label only, lowercased. The
// API stays the authority on which tenant a request belongs to; the web app
// uses the slug to render, never to authorise.
export const SUPPORTED_BASE_DOMAINS = ["earlyeagles.club", "localhost"] as const

/** Request header the proxy sets; any copy sent by the client is dropped first. */
export const TENANT_SLUG_HEADER = "x-ee-tenant-slug"

export function extractTenantSlug(host: string | null | undefined): string | null {
  if (!host) return null
  const hostOnly = stripPort(host).trim()
  if (!hostOnly) return null

  for (const baseDomain of SUPPORTED_BASE_DOMAINS) {
    const suffix = `.${baseDomain}`
    if (!hostOnly.toLowerCase().endsWith(suffix)) continue

    const prefix = hostOnly.slice(0, -suffix.length)
    // Empty: the bare base domain. A dot: a nested subdomain, not supported.
    if (!prefix || prefix.includes(".")) return null
    return prefix.toLowerCase()
  }
  return null
}

function stripPort(host: string) {
  // IPv6 hosts are bracketed; keep everything up to the closing bracket.
  if (host.startsWith("[")) {
    const close = host.indexOf("]")
    return close < 0 ? host : host.slice(0, close + 1)
  }
  const colon = host.indexOf(":")
  return colon < 0 ? host : host.slice(0, colon)
}

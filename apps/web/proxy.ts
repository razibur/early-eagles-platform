import { NextResponse, type NextRequest } from "next/server"

import { TENANT_SLUG_HEADER, extractTenantSlug } from "@/lib/tenancy/subdomain"

// Next 16 proxy (formerly middleware), P0-077.1. Resolves the tenant slug
// from the host — `alpha.earlyeagles.club`, or `alpha.localhost:3000` in dev
// — and hands it to the app as a request header the root layout reads
// (P0-077.2). No lookup, no redirect: an unknown or missing slug just means
// "no tenant" here; the API decides what that means for data.
//
// The incoming copy of the header is always deleted first, so a client can't
// pick its own tenant by sending it.
export function proxy(request: NextRequest) {
  const headers = new Headers(request.headers)
  headers.delete(TENANT_SLUG_HEADER)

  const slug = extractTenantSlug(request.headers.get("host"))
  if (slug) headers.set(TENANT_SLUG_HEADER, slug)

  return NextResponse.next({ request: { headers } })
}

export const config = {
  // Pages only: skip Next's assets, the service worker, the manifest and
  // static files (anything with an extension).
  matcher: ["/((?!_next/static|_next/image|sw\\.js|manifest\\.webmanifest|.*\\.[a-zA-Z0-9]+$).*)"],
}

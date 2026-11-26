import { NextResponse, type NextFetchEvent, type NextRequest } from "next/server"
import { clerkMiddleware } from "@clerk/nextjs/server"

import { apiForwarding, apiTarget, forwardedHeaders, isApiPath } from "@/lib/api/forwarding"
import { authMode } from "@/lib/auth/config"
import { isPublicPath } from "@/lib/auth/public-routes"
import { SIGN_IN_PATH, SIGN_UP_PATH } from "@/lib/auth/routes"
import { TENANT_SLUG_HEADER, extractTenantSlug } from "@/lib/tenancy/subdomain"

// Next 16 proxy (formerly middleware). /api/* is forwarded to the API
// (P0-071.5, ADR-0016); every other path gets two jobs, in this order:
//
// 1. Sign-in (P0-039.4). Clerk reads the session; a signed-out visitor asking
//    for anything but a public page (lib/auth/public-routes.ts) is redirected
//    to /sign-in, which brings them back afterwards. Without a Clerk key this
//    throws (authMode fails closed); EE_AUTH_DISABLED=1 builds skip it.
//
// 2. Tenant (P0-077.1). Resolves the slug from the host — `alpha.earlyeagles.club`,
//    or `alpha.localhost:3000` in dev — and hands it to the app as a request
//    header the root layout reads (P0-077.2). No lookup, no redirect: an
//    unknown or missing slug just means "no tenant"; the API decides what
//    that means for data. The incoming copy of the header is always deleted
//    first, so a client can't pick its own tenant by sending it.
const withClerk = clerkMiddleware(
  async (auth, request) => {
    if (!isPublicPath(request.nextUrl.pathname)) await auth.protect()
    return withTenant(request)
  },
  { signInUrl: SIGN_IN_PATH, signUpUrl: SIGN_UP_PATH },
)

export function proxy(request: NextRequest, event: NextFetchEvent) {
  const auth = authMode()
  if (isApiPath(request.nextUrl.pathname)) return toApi(request)
  return auth === "clerk" ? withClerk(request, event) : withTenant(request)
}

// /api/<path> → <EE_API_ORIGIN>/<path>, with the browser's Authorization
// header and the club address it called. No sign-in redirect here: the API
// is the authority and answers 401 itself; a redirect would hand fetch() an
// HTML page. Not configured → 503, never a half-working call.
function toApi(request: NextRequest) {
  const forwarding = apiForwarding()
  if (!forwarding) {
    return Response.json(
      { title: "API not configured", status: 503, detail: "Set EE_API_ORIGIN and EE_API_PROXY_KEY for the web app." },
      { status: 503, headers: { "content-type": "application/problem+json" } },
    )
  }

  const host = request.headers.get("host") ?? request.nextUrl.host
  const target = apiTarget(request.nextUrl.pathname, request.nextUrl.search, forwarding.origin)
  return NextResponse.rewrite(target, { request: { headers: forwardedHeaders(request.headers, host, forwarding.key) } })
}

function withTenant(request: NextRequest) {
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

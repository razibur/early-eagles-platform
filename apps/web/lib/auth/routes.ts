// Where Clerk's pages live (P0-039.3). ClerkProvider and the proxy both read
// these, so a link Clerk builds and a redirect the proxy sends agree.
export const SIGN_IN_PATH = "/sign-in"
export const SIGN_UP_PATH = "/sign-up"

/**
 * Where a user lands after signing in or up without a `redirect_url` (P1-035):
 * a short stop that asks the API for the user's role here and moves on to
 * {@link landingFor}. Only the API knows whether the active Clerk organization
 * is this club, so the proxy can't decide this from the token alone.
 */
export const AFTER_AUTH_PATH = "/welcome"

/** The admin home (ADR-0004). */
export const ADMIN_HOME_PATH = "/admin"

/** The admins' analytics page (ADR-0004, P1-034). */
export const INSIGHTS_PATH = "/insights"

/** Where a role lands after sign-in (ADR-0004): admins on /admin, everyone else on /. */
export function landingFor(tenantRole: string | null | undefined): string {
  return tenantRole === "tenant-admin" ? ADMIN_HOME_PATH : "/"
}

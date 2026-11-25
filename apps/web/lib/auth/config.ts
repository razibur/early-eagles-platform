// Whether Clerk sign-in is on (P0-039.1).
//
// Auth fails closed: with no publishable key the app refuses to serve pages,
// unless EE_AUTH_DISABLED=1 says, explicitly, that this build runs without
// sign-in. Only CI and the Playwright build set it; they have no Clerk
// instance. A deploy that forgot its key therefore breaks loudly instead of
// quietly serving every page to anyone.
//
// Read on the server only (proxy and server components). The publishable key
// itself is public; the secret key (CLERK_SECRET_KEY) is read by Clerk.

/** Opt-out switch for builds with no Clerk instance (CI, Playwright). */
export const AUTH_DISABLED_ENV = "EE_AUTH_DISABLED"

export type AuthMode = "clerk" | "disabled"

export function authMode(env: Record<string, string | undefined> = process.env): AuthMode {
  if (env[AUTH_DISABLED_ENV] === "1") return "disabled"

  const key = env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY?.trim()
  if (!key) {
    throw new Error(
      `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY is not set. Put the Clerk keys in apps/web/.env.local, ` +
        `or set ${AUTH_DISABLED_ENV}=1 to run without sign-in (CI only).`,
    )
  }
  if (!/^pk_(test|live)_\S+$/.test(key)) {
    throw new Error("NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY does not look like a Clerk publishable key (pk_test_… / pk_live_…).")
  }
  return "clerk"
}

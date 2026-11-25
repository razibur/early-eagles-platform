// Where Clerk's pages live (P0-039.3). ClerkProvider and the proxy both read
// these, so a link Clerk builds and a redirect the proxy sends agree.
export const SIGN_IN_PATH = "/sign-in"
export const SIGN_UP_PATH = "/sign-up"

/** Where a user lands after signing in or up without a `redirect_url`. */
export const AFTER_AUTH_PATH = "/"

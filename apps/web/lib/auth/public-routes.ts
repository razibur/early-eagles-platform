import { SIGN_IN_PATH, SIGN_UP_PATH } from "./routes"

// Pages a signed-out visitor may open (P0-039.4); every other page sends them
// to sign-in. Static files, the service worker and the manifest never reach
// the proxy at all (see its matcher), so they need no entry here.
//
// Prefix entries cover their sub-paths on a segment boundary: /sign-in covers
// /sign-in/factor-one but not /sign-in-please.
const PUBLIC_PREFIXES = [SIGN_IN_PATH, SIGN_UP_PATH, "/cookies"] as const

export function isPublicPath(pathname: string): boolean {
  const path = pathname.toLowerCase()
  return PUBLIC_PREFIXES.some((prefix) => path === prefix || path.startsWith(`${prefix}/`))
}

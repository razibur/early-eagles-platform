import type { StateStorage } from "zustand/middleware"

// A Zustand persist storage backed by a first-party cookie (P0-072). Used
// where the record is itself about cookies (consent) and should be readable
// server-side later without a client round trip. Values are URI-encoded JSON.
//
// Wrapped in try/catch: cookies can be disabled, and a storage failure must
// leave the app working with in-memory state.
export function cookieStorage({ maxAgeDays }: { maxAgeDays: number }): StateStorage {
  return {
    getItem(name) {
      try {
        const match = document.cookie.split("; ").find((part) => part.startsWith(`${name}=`))
        return match ? decodeURIComponent(match.slice(name.length + 1)) : null
      } catch {
        return null
      }
    },
    setItem(name, value) {
      try {
        const secure = location.protocol === "https:" ? "; Secure" : ""
        document.cookie = `${name}=${encodeURIComponent(value)}; Max-Age=${maxAgeDays * 86400}; Path=/; SameSite=Lax${secure}`
      } catch {
        // Not persisted; state still holds for this page view.
      }
    },
    removeItem(name) {
      try {
        document.cookie = `${name}=; Max-Age=0; Path=/; SameSite=Lax`
      } catch {
        // Nothing to clean up.
      }
    },
  }
}

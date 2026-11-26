"use client"

import { createContext, useContext, useMemo } from "react"
import { useAuth } from "@clerk/nextjs"

import { createApiFetch, type ApiFetch } from "@/lib/api/client"

// Hands components an API client (P0-071.2). Two providers, picked by the
// root layout: ClerkApiProvider attaches the session token from Clerk;
// AnonymousApiProvider (builds without Clerk) sends none, so the API answers
// protected calls with 401 as it would for any signed-out visitor.
const ApiFetchContext = createContext<ApiFetch | null>(null)

export function ClerkApiProvider({ children }: { children: React.ReactNode }) {
  const { getToken } = useAuth()
  // Clerk caches the short-lived token and refreshes it before it expires,
  // so asking on every call is cheap.
  const apiFetch = useMemo(() => createApiFetch(() => getToken()), [getToken])
  return <ApiFetchContext.Provider value={apiFetch}>{children}</ApiFetchContext.Provider>
}

const anonymousApiFetch = createApiFetch(async () => null)

export function AnonymousApiProvider({ children }: { children: React.ReactNode }) {
  return <ApiFetchContext.Provider value={anonymousApiFetch}>{children}</ApiFetchContext.Provider>
}

/** The API client for this page; use it inside TanStack Query's queryFn / mutationFn. */
export function useApiFetch(): ApiFetch {
  const apiFetch = useContext(ApiFetchContext)
  if (!apiFetch) throw new Error("useApiFetch must be used inside ClerkApiProvider or AnonymousApiProvider (root layout).")
  return apiFetch
}

"use client"

import { QueryClient, QueryClientProvider, isServer } from "@tanstack/react-query"

// TanStack Query for client-side server state (P0-071.1).
//
// The client is created the way TanStack's App Router guide prescribes:
//   - on the server, a fresh client per request, so one member's cached data
//     can never be rendered into another member's (or another tenant's) page;
//   - in the browser, one client for the life of the tab, so a re-render or a
//     suspense boundary never throws the cache away.
//
// staleTime 60 s: with server rendering, data fetched on the server would
// otherwise be refetched the moment the page hydrates.
function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 60 * 1000 },
    },
  })
}

let browserQueryClient: QueryClient | undefined

function getQueryClient() {
  if (isServer) return makeQueryClient()
  browserQueryClient ??= makeQueryClient()
  return browserQueryClient
}

export function QueryProvider({ children }: { children: React.ReactNode }) {
  return <QueryClientProvider client={getQueryClient()}>{children}</QueryClientProvider>
}

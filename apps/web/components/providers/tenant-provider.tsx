"use client"

import { createContext, useContext } from "react"

// The tenant this page renders for (P0-077.2): the slug proxy.ts resolved
// from the subdomain, read by the root layout and handed down here. `null`
// on a bare host (localhost, earlyeagles.club): no tenant.
//
// Render-only. Data access is scoped by the API from the same subdomain, so
// nothing here can widen what a request is allowed to see.
export type Tenant = { slug: string | null }

const TenantContext = createContext<Tenant>({ slug: null })

export function TenantProvider({ slug, children }: { slug: string | null; children: React.ReactNode }) {
  return <TenantContext.Provider value={{ slug }}>{children}</TenantContext.Provider>
}

export function useTenant(): Tenant {
  return useContext(TenantContext)
}

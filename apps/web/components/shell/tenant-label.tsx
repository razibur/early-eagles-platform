"use client"

import { useTenant } from "@/components/providers/tenant-provider"

// The small line under the wordmark: the tenant's slug when the page is on a
// tenant subdomain, otherwise the generic "Club HQ" (P0-077.2). Shows the
// slug, not a display name: names need a tenant lookup against the API,
// which arrives with the authenticated fetch client (P0-071.2).
export function TenantLabel() {
  const { slug } = useTenant()

  return (
    <span data-tenant-slug={slug ?? undefined} className="text-[0.5625rem] font-bold uppercase tracking-[0.24em] text-muted-foreground">
      {slug ?? "Club HQ"}
    </span>
  )
}

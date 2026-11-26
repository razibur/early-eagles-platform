"use client"

import { useQuery } from "@tanstack/react-query"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError } from "@/lib/api/client"

// Shows its children to the club's admins only (P1-017). This is for the
// screen, not for safety: every admin endpoint already answers 403 to anyone
// else. It asks /me, the same query /account uses.

type Me = { tenantRole: "tenant-admin" | "member" | null }

export function AdminGate({ children }: { children: React.ReactNode }) {
  const apiFetch = useApiFetch()
  const me = useQuery({ queryKey: ["me"], queryFn: () => apiFetch<Me>("/me"), retry: false })

  if (me.isPending) {
    return <span aria-label="Checking your role" className="mt-10 block h-40 w-full animate-pulse rounded-lg bg-muted motion-reduce:animate-none" />
  }
  if (me.data?.tenantRole === "tenant-admin") return <>{children}</>

  return (
    <p className="mt-10 rounded-lg bg-card p-5 text-muted-foreground ring-1 ring-rule md:p-8">
      {me.isError
        ? me.error instanceof ApiError && me.error.status === 401
          ? "Sign in as a club admin to use this page."
          : "Couldn't reach the club right now."
        : "This page is for the club's admins."}
    </p>
  )
}

"use client"

import { useQuery } from "@tanstack/react-query"
import { useRouter } from "next/navigation"
import { useEffect } from "react"

import { useApiFetch } from "@/components/providers/api-provider"
import { landingFor } from "@/lib/auth/routes"

// The stop after sign-in (P1-035): ask /me for the role in this club, then
// replace this page with the right home, so Back doesn't return here. A
// fresh answer is required (`refetchOnMount: "always"`): a cached /me could
// belong to whoever used this tab before. Any error goes to the member home,
// which explains itself.

type Me = { tenantRole: "tenant-admin" | "member" | null }

export function RoleLanding() {
  const apiFetch = useApiFetch()
  const router = useRouter()
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => apiFetch<Me>("/me"),
    retry: false,
    refetchOnMount: "always",
  })

  const settled = !me.isFetching && (me.isSuccess || me.isError)
  const target = me.isSuccess ? landingFor(me.data.tenantRole) : "/"

  useEffect(() => {
    if (settled) router.replace(target)
  }, [settled, target, router])

  return (
    <p role="status" className="label animate-pulse text-muted-foreground motion-reduce:animate-none">
      Opening your club…
    </p>
  )
}

"use client"

import { useQuery } from "@tanstack/react-query"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError } from "@/lib/api/client"

// What the API knows about the signed-in person at this club address
// (P0-071.3): the first page that renders data fetched with the session
// token. Two calls: /whoami (which club this address is) and /me (who you
// are, and your role in that club).

type WhoAmI = { tenantId: string; slug: string }
type Me = {
  userId: string
  sessionId: string | null
  organization: { id: string; slug: string | null; role: string | null } | null
  tenantRole: "tenant-admin" | "member" | null
}

const roleLabel: Record<NonNullable<Me["tenantRole"]>, string> = {
  "tenant-admin": "Club admin",
  member: "Member",
}

export function AccountPanel() {
  const apiFetch = useApiFetch()
  const club = useQuery({
    queryKey: ["whoami"],
    queryFn: () => apiFetch<WhoAmI>("/whoami"),
    retry: false,
  })
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => apiFetch<Me>("/me"),
    retry: false,
  })

  return (
    <dl className="mt-10 divide-y divide-rule rounded-lg bg-card ring-1 ring-rule" aria-busy={club.isPending || me.isPending}>
      <Row label="Club at this address">
        {club.isPending ? (
          <Pending />
        ) : club.data ? (
          <span className="font-display text-2xl font-extrabold uppercase tracking-tight text-foreground">{club.data.slug}</span>
        ) : (
          <Muted>{noClubMessage(club.error)}</Muted>
        )}
      </Row>
      <Row label="Your role here">
        {me.isPending ? (
          <Pending />
        ) : me.data ? (
          me.data.tenantRole ? (
            <span className="inline-flex items-center gap-2 text-foreground">
              <span aria-hidden className={`size-2 rounded-full ${me.data.tenantRole === "tenant-admin" ? "bg-shuttle" : "bg-primary"}`} />
              {roleLabel[me.data.tenantRole]}
            </span>
          ) : (
            <Muted>Not a member of this club yet. Your club&apos;s admin adds you.</Muted>
          )
        ) : (
          <Muted>{signedOutMessage(me.error)}</Muted>
        )}
      </Row>
      <Row label="Signed in as">
        {me.isPending ? <Pending /> : me.data ? <code className="text-sm text-muted-foreground">{me.data.userId}</code> : <Muted>—</Muted>}
      </Row>
    </dl>
  )
}

function noClubMessage(error: unknown) {
  if (error instanceof ApiError && (error.status === 400 || error.status === 404)) return "No club at this address."
  return "Couldn't reach the club right now."
}

function signedOutMessage(error: unknown) {
  if (error instanceof ApiError && error.status === 401) return "Not signed in."
  return "Couldn't reach the club right now."
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1 px-5 py-4 sm:grid-cols-[12rem_1fr] sm:items-center sm:gap-6">
      <dt className="label text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-words">{children}</dd>
    </div>
  )
}

function Muted({ children }: { children: React.ReactNode }) {
  return <span className="text-muted-foreground">{children}</span>
}

function Pending() {
  return <span aria-label="Loading" className="block h-5 w-32 animate-pulse rounded bg-muted motion-reduce:animate-none" />
}

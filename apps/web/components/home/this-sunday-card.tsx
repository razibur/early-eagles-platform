"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useId, useState } from "react"

import { DuesStrip } from "@/components/home/dues-strip"
import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError, type ApiFetch } from "@/lib/api/client"
import {
  describeSession,
  isSessionList,
  isoWeekAfter,
  isoWeekOf,
  nextSession,
  recount,
  type RsvpAnswer,
  type SessionDetails,
  type SessionSummary,
} from "@/lib/sessions/next-session"
import { cn } from "@/lib/utils"

// "This Sunday" on the member home (P1-014): the next session as a match-day
// fixture card, with the member's answer one tap away. Reads
// GET /sessions?week= (this week, then next week once this week's sessions
// have started) and GET /sessions/{id}; answers with PUT /sessions/{id}/rsvp.
// Dues for the session sit underneath (DuesStrip, P1-016).

const NEXT_KEY = ["sessions", "next"] as const
const detailsKey = (id: string) => ["sessions", id] as const

async function fetchNext(apiFetch: ApiFetch): Promise<SessionSummary | null> {
  const now = new Date()
  for (const week of [isoWeekOf(now), isoWeekAfter(now)]) {
    const list = await apiFetch<unknown>(`/sessions?week=${week}`)
    if (!isSessionList(list)) throw new Error("Unexpected session list")
    const next = nextSession(list, now)
    if (next) return next
  }
  return null
}

const answers: { value: RsvpAnswer; label: string; hint: string }[] = [
  { value: "Yes", label: "I'm in", hint: "Count me on court" },
  { value: "Maybe", label: "Maybe", hint: "Not sure yet" },
  { value: "No", label: "Can't make it", hint: "Sitting this one out" },
]

export function ThisSundayCard() {
  const apiFetch = useApiFetch()
  const next = useQuery({ queryKey: NEXT_KEY, queryFn: () => fetchNext(apiFetch), retry: false })
  const id = next.data?.id
  const details = useQuery({
    queryKey: detailsKey(id ?? ""),
    queryFn: () => apiFetch<SessionDetails>(`/sessions/${id}`),
    enabled: Boolean(id),
    retry: false,
  })

  if (next.isPending || (id && details.isPending)) return <CardSkeleton />
  if (next.isError) return <CardNotice>{noticeFor(next.error)}</CardNotice>
  if (!next.data) {
    return (
      <CardNotice title="No session on the board">
        Nothing scheduled for this week or next. Your club&apos;s admin posts each Sunday.
      </CardNotice>
    )
  }
  if (details.isError || !details.data) return <CardNotice>{noticeFor(details.error)}</CardNotice>
  return <Fixture session={details.data} />
}

function Fixture({ session }: { session: SessionDetails }) {
  const apiFetch = useApiFetch()
  const queryClient = useQueryClient()
  const headingId = useId()
  const [problem, setProblem] = useState<string | null>(null)
  const when = describeSession(session)
  const cancelled = session.status === "Cancelled"

  const rsvp = useMutation({
    mutationFn: (response: RsvpAnswer) =>
      apiFetch<void>(`/sessions/${session.id}/rsvp`, { method: "PUT", json: { response } }),
    onMutate: async (response) => {
      setProblem(null)
      await queryClient.cancelQueries({ queryKey: detailsKey(session.id) })
      const before = queryClient.getQueryData<SessionDetails>(detailsKey(session.id))
      if (before) {
        queryClient.setQueryData<SessionDetails>(detailsKey(session.id), {
          ...before,
          myRsvp: response,
          rsvpCounts: recount(before.rsvpCounts, before.myRsvp, response),
        })
      }
      return { before }
    },
    onError: (error, _response, context) => {
      if (context?.before) queryClient.setQueryData(detailsKey(session.id), context.before)
      setProblem(
        error instanceof ApiError && error.status === 409
          ? "Answers are closed: the session has started or was called off."
          : "Couldn't save your answer. Try again.",
      )
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: detailsKey(session.id) }),
  })

  return (
    <article
      aria-labelledby={headingId}
      className="relative overflow-hidden rounded-lg bg-card ring-1 ring-rule"
    >
      {/* Court lines behind the fixture — a quiet nod to the service box */}
      <svg aria-hidden className="pointer-events-none absolute inset-0 h-full w-full text-rule/50" preserveAspectRatio="none" viewBox="0 0 100 60">
        <path d="M0 20H100M0 40H100M62 0V60" stroke="currentColor" strokeWidth="0.2" fill="none" vectorEffect="non-scaling-stroke" />
      </svg>
      <span aria-hidden className={cn("absolute inset-x-0 top-0 h-1", cancelled ? "bg-live" : "bg-primary")} />

      <div className="relative grid gap-8 p-5 md:grid-cols-12 md:gap-10 md:p-8">
        {/* Date block — the fixture board */}
        <div className="md:col-span-7">
          <span className="label label-accent text-primary">{cancelled ? "Called off" : `This ${when.weekday}`}</span>
          <h2 id={headingId} className="mt-5 flex items-end gap-4">
            <time dateTime={when.dateTimeAttr} className="contents">
              <span className={cn("display figures text-[7rem] leading-[0.8] md:text-[9rem]", cancelled ? "text-muted-foreground line-through decoration-live decoration-4" : "text-foreground")}>
                {when.day}
              </span>
              <span className="flex flex-col pb-2">
                <span className="display text-3xl uppercase text-foreground md:text-4xl">{when.month}</span>
                <span className="label text-muted-foreground">{when.weekday}</span>
              </span>
            </time>
          </h2>
          <dl className="mt-6 grid grid-cols-3 gap-4 border-t border-rule pt-4">
            <Fact label="Start">{when.time}</Fact>
            <Fact label="Venue" className="col-span-2 sm:col-span-1">{session.venue}</Fact>
            <Fact label="Fee" className="col-span-3 sm:col-span-1">{when.fee}</Fact>
          </dl>
        </div>

        {/* Tally — the scoreboard */}
        <div className="md:col-span-5 md:border-l md:border-rule md:pl-10">
          <span className="label text-muted-foreground">Who&apos;s coming</span>
          <dl className="mt-4 grid grid-cols-3 divide-x divide-rule rounded-md bg-background/60 ring-1 ring-rule">
            <Tally label="In" value={session.rsvpCounts.yes} tone="text-primary" />
            <Tally label="Maybe" value={session.rsvpCounts.maybe} tone="text-shuttle" />
            <Tally label="Out" value={session.rsvpCounts.no} tone="text-muted-foreground" />
          </dl>
          {cancelled ? (
            <p className="mt-6 text-sm leading-relaxed text-muted-foreground">
              This session won&apos;t happen. Watch the announcements for the next one.
            </p>
          ) : null}
        </div>
      </div>

      {cancelled ? null : (
        <div className="relative border-t border-rule bg-paper/70 p-5 md:px-8">
          <fieldset>
            <legend className="label text-muted-foreground">
              {session.myRsvp ? "Your answer — tap to change" : "Are you playing?"}
            </legend>
            <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3 sm:gap-3">
              {answers.map((a) => {
                const chosen = session.myRsvp === a.value
                return (
                  <button
                    key={a.value}
                    type="button"
                    aria-pressed={chosen}
                    disabled={rsvp.isPending}
                    onClick={() => rsvp.mutate(a.value)}
                    className={cn(
                      "group flex min-h-14 items-center justify-between gap-3 rounded-md px-4 py-3 text-left ring-1 transition-[background-color,box-shadow,transform] duration-150 active:scale-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait motion-reduce:transition-none motion-reduce:active:scale-100",
                      chosen
                        ? a.value === "Yes"
                          ? "bg-primary text-primary-foreground ring-primary"
                          : a.value === "Maybe"
                            ? "bg-shuttle text-primary-foreground ring-shuttle"
                            : "bg-foreground text-background ring-foreground"
                        : "bg-card text-foreground ring-rule hover:ring-foreground/40",
                    )}
                  >
                    <span className="flex flex-col">
                      <span className="display text-2xl uppercase">{a.label}</span>
                      <span className={cn("text-xs", chosen ? "opacity-80" : "text-muted-foreground")}>{a.hint}</span>
                    </span>
                    <span
                      aria-hidden
                      className={cn(
                        "grid size-6 shrink-0 place-items-center rounded-full ring-1 text-sm font-bold",
                        chosen ? "bg-background/20 ring-current" : "ring-rule text-transparent",
                      )}
                    >
                      ✓
                    </span>
                  </button>
                )
              })}
            </div>
          </fieldset>
          <p role="status" aria-live="polite" className="mt-3 min-h-5 text-sm text-live">
            {problem}
          </p>
        </div>
      )}

      <DuesStrip session={session} queryKey={detailsKey(session.id)} />
    </article>
  )
}

function Fact({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("min-w-0", className)}>
      <dt className="label text-muted-foreground">{label}</dt>
      <dd className="mt-1 truncate font-semibold text-foreground">{children}</dd>
    </div>
  )
}

function Tally({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className="flex flex-col items-center gap-1 px-2 py-4">
      <dd className={cn("display figures text-5xl", tone)}>{value}</dd>
      <dt className="label text-muted-foreground">{label}</dt>
    </div>
  )
}

function CardNotice({ title = "This Sunday", children }: { title?: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg bg-card p-5 ring-1 ring-rule md:p-8">
      <span className="label label-accent text-primary">{title}</span>
      <p className="mt-5 max-w-prose text-muted-foreground">{children}</p>
    </section>
  )
}

function CardSkeleton() {
  return (
    <div aria-busy aria-label="Loading this Sunday" className="rounded-lg bg-card p-5 ring-1 ring-rule md:p-8">
      <span className="block h-3 w-28 animate-pulse rounded bg-muted motion-reduce:animate-none" />
      <span className="mt-6 block h-28 w-48 animate-pulse rounded bg-muted motion-reduce:animate-none" />
      <span className="mt-6 block h-14 w-full animate-pulse rounded bg-muted motion-reduce:animate-none" />
    </div>
  )
}

function noticeFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Sign in to see this Sunday."
    if (error.status === 403) return "You're not on this club's roster yet. Your club's admin adds you."
    if (error.status === 400 || error.status === 404) return "Sessions aren't open at this address yet."
  }
  return "Couldn't reach the club right now."
}

"use client"

import { useMutation, useQueryClient } from "@tanstack/react-query"
import { useId, useState } from "react"

import { useApiFetch } from "@/components/providers/api-provider"
import { ApiError } from "@/lib/api/client"
import { formatDollars, type SessionDetails } from "@/lib/sessions/next-session"
import { cn } from "@/lib/utils"

// Dues for this session on the "This Sunday" card (P1-016). Dues are tracked,
// not collected: the member pays by Zelle outside the app, then says so here
// with the name on the transfer and its reference. The admin confirms or
// rejects it (P1-009, P1-009.2); after a rejection the member reports again.

/** The API's limit for both fields (SessionPayment.SenderNameMaxLength / ReferenceMaxLength). */
export const PAYMENT_FIELD_MAX = 100

type Props = { session: SessionDetails; queryKey: readonly unknown[] }

export function DuesStrip({ session, queryKey }: Props) {
  const payment = session.myPayment
  const [open, setOpen] = useState(false)
  const amount = formatDollars(payment?.amountCents ?? session.feeCents)

  if (session.status === "Cancelled" || (session.feeCents === 0 && !payment)) return null

  return (
    <section aria-label="Your dues" className="relative border-t border-rule p-5 md:px-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span aria-hidden className={cn("size-2.5 rounded-full", dot(payment?.status))} />
          <div>
            <span className="label text-muted-foreground">Dues · {amount}</span>
            <p className="font-semibold text-foreground">{headline(payment?.status)}</p>
          </div>
        </div>
        {canReport(payment?.status) && !open ? (
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="min-h-11 rounded-md px-4 text-sm font-bold uppercase tracking-[0.12em] text-foreground ring-1 ring-rule transition-shadow hover:ring-foreground/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {payment?.status === "Rejected" ? "Report again" : "I've paid"}
          </button>
        ) : null}
      </div>

      {payment?.status === "Rejected" && payment.rejectionReason ? (
        <p className="mt-3 border-l-2 border-live pl-3 text-sm text-muted-foreground">
          <span className="font-semibold text-foreground">Not confirmed: </span>
          {payment.rejectionReason}
        </p>
      ) : null}

      {open && canReport(payment?.status) ? (
        <ReportForm sessionId={session.id} amount={amount} queryKey={queryKey} onDone={() => setOpen(false)} />
      ) : null}
    </section>
  )
}

function ReportForm({ sessionId, amount, queryKey, onDone }: { sessionId: string; amount: string; queryKey: readonly unknown[]; onDone: () => void }) {
  const apiFetch = useApiFetch()
  const queryClient = useQueryClient()
  const nameId = useId()
  const refId = useId()
  const [senderName, setSenderName] = useState("")
  const [reference, setReference] = useState("")
  const [problem, setProblem] = useState<string | null>(null)

  const report = useMutation({
    mutationFn: () =>
      apiFetch<{ paymentId: string }>(`/sessions/${sessionId}/payment-report`, {
        method: "POST",
        json: { senderName: senderName.trim(), reference: reference.trim() },
      }),
    onMutate: () => setProblem(null),
    onSuccess: () => onDone(),
    onError: (error) => setProblem(reportProblem(error)),
    onSettled: () => queryClient.invalidateQueries({ queryKey }),
  })

  return (
    <form
      className="mt-4 grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end"
      onSubmit={(e) => {
        e.preventDefault()
        if (!senderName.trim() || !reference.trim()) {
          setProblem("Fill in both: the name on the transfer and its reference.")
          return
        }
        report.mutate()
      }}
    >
      <p className="text-sm text-muted-foreground sm:col-span-3">
        Send {amount} by Zelle first, then tell the club here. Your admin checks it against the transfer.
      </p>
      <Field id={nameId} label="Name on the transfer" value={senderName} onChange={setSenderName} autoComplete="name" />
      <Field id={refId} label="Zelle reference" value={reference} onChange={setReference} autoComplete="off" />
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={report.isPending}
          className="min-h-11 flex-1 rounded-md bg-primary px-5 text-sm font-bold uppercase tracking-[0.12em] text-primary-foreground transition-opacity focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-wait disabled:opacity-70"
        >
          {report.isPending ? "Sending…" : "Send"}
        </button>
        <button
          type="button"
          onClick={onDone}
          className="min-h-11 rounded-md px-4 text-sm font-semibold text-muted-foreground ring-1 ring-rule hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Cancel
        </button>
      </div>
      <p role="status" aria-live="polite" className="min-h-5 text-sm text-live sm:col-span-3">
        {problem}
      </p>
    </form>
  )
}

function Field({ id, label, value, onChange, autoComplete }: { id: string; label: string; value: string; onChange: (v: string) => void; autoComplete: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="label text-muted-foreground">
        {label}
      </label>
      <input
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        maxLength={PAYMENT_FIELD_MAX}
        required
        autoComplete={autoComplete}
        className="min-h-11 rounded-md bg-background px-3 text-foreground ring-1 ring-rule placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      />
    </div>
  )
}

type Status = NonNullable<SessionDetails["myPayment"]>["status"] | undefined

function canReport(status: Status) {
  return status === undefined || status === "Rejected"
}

function headline(status: Status) {
  switch (status) {
    case "Confirmed":
      return "Paid. Thank you."
    case "Reported":
      return "Reported, waiting for your admin to confirm."
    case "Rejected":
      return "Your report wasn't confirmed."
    default:
      return "Not paid yet."
  }
}

function dot(status: Status) {
  switch (status) {
    case "Confirmed":
      return "bg-primary"
    case "Reported":
      return "bg-shuttle"
    case "Rejected":
      return "bg-live"
    default:
      return "bg-muted-foreground"
  }
}

function reportProblem(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return "This session can't take a payment report now: it's already reported, free or called off."
    if (error.status === 400) return "Check both fields: each needs some text, up to 100 characters."
  }
  return "Couldn't send your report. Try again."
}

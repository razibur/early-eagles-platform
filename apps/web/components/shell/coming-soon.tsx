import Link from "next/link"

import { navItem } from "@/lib/navigation"

// Placeholder for a section whose page hasn't been built (P0-070.2), so the
// navigation never leads to a 404. Says which phase delivers it and what it
// will hold, from the same nav list the bars use.
export function ComingSoon({ href }: { href: string }) {
  const { label, icon: Icon, phase } = navItem(href)

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col px-5 py-12 md:px-8 md:py-20 lg:px-12">
      <div className="relative max-w-2xl">
        <span className="label label-accent text-primary">Phase {phase?.number ?? "—"} · In the plan</span>
        <h1 className="display mt-6 flex items-center gap-4 text-6xl text-foreground md:text-8xl">
          <Icon className="size-10 shrink-0 text-primary md:size-14" strokeWidth={1.5} aria-hidden />
          {label}
        </h1>
        <p className="mt-6 max-w-prose text-lg leading-relaxed text-muted-foreground">{phase?.summary}</p>
        <div aria-hidden className="mt-10 flex h-1.5 max-w-sm gap-1">
          <span className="flex-2 bg-primary" />
          <span className="flex-1 bg-destructive" />
          <span className="flex-6 bg-rule" />
        </div>
        <Link
          href="/"
          className="mt-10 inline-flex items-center gap-2 text-[0.6875rem] font-bold uppercase tracking-[0.18em] text-muted-foreground transition-colors hover:text-foreground"
        >
          ← Back to the dashboard
        </Link>
      </div>
    </main>
  )
}

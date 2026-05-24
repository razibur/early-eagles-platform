import { cn } from "@/lib/utils"

type MastheadProps = {
  edition: string
  established?: string
  liveLabel?: string
  className?: string
}

export function Masthead({
  edition,
  established = "Est. 2024",
  liveLabel = "Live · Sunday Edition",
  className,
}: MastheadProps) {
  return (
    <header className={cn("relative", className)}>
      {/* Top status strip */}
      <div className="flex items-center justify-between gap-4 border-b border-rule pb-3 text-[0.6875rem] font-bold uppercase tracking-[0.22em] text-muted-foreground">
        <div className="flex items-center gap-2">
          <span className="live-dot" aria-hidden />
          <span className="text-foreground">{liveLabel}</span>
        </div>
        <span className="hidden sm:inline">{established}</span>
        <span className="figures">{edition}</span>
      </div>

      {/* Wordmark band */}
      <div className="flex items-end justify-between gap-6 pt-6 md:pt-10">
        <div className="flex items-center gap-4">
          <span
            aria-hidden
            className="hidden sm:block h-16 w-16 rounded-full bg-foreground/5 ring-1 ring-rule"
          >
            {/* Logo slot — drop apps/web/public/logo.png to render */}
          </span>
          <div>
            <span className="label text-primary block">Badminton Club HQ</span>
            <h1 className="display text-6xl sm:text-7xl md:text-8xl text-foreground">
              Early Eagles
            </h1>
          </div>
        </div>
        <div className="hidden md:flex flex-col items-end text-right text-xs uppercase tracking-[0.2em] text-muted-foreground">
          <span>Tenant 0</span>
          <span className="text-primary">Members 35</span>
          <span>Court · 2</span>
        </div>
      </div>

      {/* Accent strike under masthead */}
      <div className="mt-6 flex h-1.5 gap-1">
        <span className="flex-2 bg-primary" />
        <span className="flex-1 bg-destructive" />
        <span className="flex-6 bg-rule" />
      </div>
    </header>
  )
}

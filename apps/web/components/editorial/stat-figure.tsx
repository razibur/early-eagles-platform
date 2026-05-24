import { cn } from "@/lib/utils"

type StatFigureProps = {
  label: string
  value: string
  unit?: string
  delta?: { value: string; direction: "up" | "down" | "flat" }
  caption?: string
  className?: string
  emphasis?: "primary" | "neutral"
}

export function StatFigure({
  label,
  value,
  unit,
  delta,
  caption,
  className,
  emphasis = "neutral",
}: StatFigureProps) {
  return (
    <article
      className={cn(
        "group relative flex flex-col gap-3 rounded-md bg-card/60 px-5 py-5 ring-1 ring-rule",
        emphasis === "primary" && "ring-primary/40 bg-primary/5",
        className,
      )}
    >
      {/* Side accent stripe — jersey trim */}
      <span
        aria-hidden
        className={cn(
          "absolute left-0 top-3 bottom-3 w-0.75 rounded-r",
          emphasis === "primary" ? "bg-primary" : "bg-rule",
        )}
      />
      <span className="label">{label}</span>
      <div className="flex items-baseline gap-2">
        <span className="display figures text-6xl md:text-7xl text-foreground tracking-tight">
          {value}
        </span>
        {unit ? (
          <span className="text-sm font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            {unit}
          </span>
        ) : null}
      </div>
      {delta ? (
        <div
          className={cn(
            "figures inline-flex items-center gap-1.5 text-sm font-semibold",
            delta.direction === "up" && "text-primary",
            delta.direction === "down" && "text-destructive",
            delta.direction === "flat" && "text-muted-foreground",
          )}
        >
          <span aria-hidden>
            {delta.direction === "up" ? "▲" : delta.direction === "down" ? "▼" : "—"}
          </span>
          <span>{delta.value}</span>
          <span className="text-muted-foreground font-normal"> vs. prior</span>
        </div>
      ) : null}
      {caption ? (
        <p className="text-sm leading-snug text-muted-foreground max-w-[30ch]">
          {caption}
        </p>
      ) : null}
    </article>
  )
}

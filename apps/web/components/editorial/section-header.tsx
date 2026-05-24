import { cn } from "@/lib/utils"

type SectionHeaderProps = {
  kicker: string
  title: string
  meta?: string
  className?: string
}

export function SectionHeader({ kicker, title, meta, className }: SectionHeaderProps) {
  return (
    <div className={cn("flex flex-col gap-3", className)}>
      <div className="flex items-baseline justify-between gap-4">
        <span className="label label-accent">{kicker}</span>
        {meta ? (
          <span className="label figures opacity-80">{meta}</span>
        ) : null}
      </div>
      <h2 className="display text-3xl md:text-5xl text-foreground">{title}</h2>
    </div>
  )
}

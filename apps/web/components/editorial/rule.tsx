import { cn } from "@/lib/utils"

export function Rule({
  className,
  weight = "hair",
  tone = "default",
}: {
  className?: string
  weight?: "hair" | "fine" | "bold"
  tone?: "default" | "primary"
}) {
  return (
    <hr
      aria-hidden
      className={cn(
        "border-0",
        tone === "primary" ? "bg-primary" : "bg-rule",
        weight === "hair" && "h-px",
        weight === "fine" && "h-[1.5px]",
        weight === "bold" && "h-0.75",
        className,
      )}
    />
  )
}

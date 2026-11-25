import type { ReactNode } from "react"

// The sign-in / sign-up screen (P0-039.3): a court that draws itself on one
// side, Clerk's card on the other. On a phone the court is left out, so the
// form sits right under the headline, within thumb reach.
export function AuthStage({
  eyebrow,
  title,
  lede,
  children,
}: {
  eyebrow: string
  title: ReactNode
  lede: string
  children: ReactNode
}) {
  return (
    <main className="relative z-10 mx-auto grid w-full max-w-6xl flex-1 items-center gap-10 px-5 py-10 md:grid-cols-[1.1fr_1fr] md:gap-16 md:px-8 md:py-20 lg:px-12">
      <section className="animate-in fade-in slide-in-from-bottom-2 duration-500 motion-reduce:animate-none">
        <span className="label label-accent text-primary">{eyebrow}</span>
        <h1 className="display mt-6 text-5xl text-foreground sm:text-6xl lg:text-8xl">{title}</h1>
        <p className="mt-5 max-w-md text-base leading-relaxed text-muted-foreground md:text-lg">{lede}</p>
        <Court className="mt-12 hidden w-full max-w-md md:block" />
        <div aria-hidden className="mt-6 hidden h-1.5 max-w-xs gap-1 md:flex">
          <span className="flex-2 bg-primary" />
          <span className="flex-1 bg-destructive" />
          <span className="flex-6 bg-rule" />
        </div>
      </section>

      <section className="flex justify-center animate-in fade-in slide-in-from-bottom-3 delay-150 duration-500 fill-mode-both motion-reduce:animate-none md:justify-end">
        {children}
      </section>
    </main>
  )
}

// A doubles court to scale (13.4 m × 6.1 m, in centimetres), seen from above
// with the net across the middle. The near-right service box is lit: the
// first serve of a Sunday morning.
function Court({ className }: { className?: string }) {
  const W = 1340
  const H = 610
  const net = W / 2
  const shortService = 198 // short service line, from the net
  const longService = 76 // doubles long service line, from the back
  const side = 46 // singles sideline, inset from the doubles one

  const lines: { d: string; len: number }[] = [
    { d: `M0 0H${W}V${H}H0Z`, len: 2 * (W + H) },
    { d: `M0 ${side}H${W}`, len: W },
    { d: `M0 ${H - side}H${W}`, len: W },
    { d: `M${net - shortService} 0V${H}`, len: H },
    { d: `M${net + shortService} 0V${H}`, len: H },
    { d: `M${longService} 0V${H}`, len: H },
    { d: `M${W - longService} 0V${H}`, len: H },
    { d: `M0 ${H / 2}H${net - shortService}`, len: net - shortService },
    { d: `M${net + shortService} ${H / 2}H${W}`, len: net - shortService },
  ]

  return (
    <svg viewBox={`-8 -8 ${W + 16} ${H + 16}`} className={className} role="img" aria-label="A badminton court">
      <rect
        className="court-box fill-primary/25"
        style={{ "--court-delay": "900ms" } as React.CSSProperties}
        x={net + shortService}
        y={H / 2}
        width={W - longService - net - shortService}
        height={H / 2 - side}
      />
      <g fill="none" strokeWidth={5} strokeLinecap="square" className="stroke-rule">
        {lines.map((line, i) => (
          <path
            key={line.d}
            d={line.d}
            className="court-line"
            style={{ "--court-len": line.len, "--court-delay": `${i * 60}ms` } as React.CSSProperties}
          />
        ))}
      </g>
      {/* The net: drawn last, in the foreground color, a touch heavier. */}
      <path
        d={`M${net} -8V${H + 8}`}
        className="court-line stroke-foreground"
        strokeWidth={8}
        style={{ "--court-len": H + 16, "--court-delay": "600ms" } as React.CSSProperties}
      />
      <circle
        className="court-box fill-shuttle"
        style={{ "--court-delay": "1100ms" } as React.CSSProperties}
        cx={net + shortService + 230}
        cy={H / 2 + 140}
        r={16}
      />
    </svg>
  )
}

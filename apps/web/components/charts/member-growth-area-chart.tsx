"use client"

import { Area, AreaChart, CartesianGrid, XAxis } from "recharts"

import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const data = [
  { month: "Nov", members: 24 },
  { month: "Dec", members: 26 },
  { month: "Jan", members: 28 },
  { month: "Feb", members: 30 },
  { month: "Mar", members: 32 },
  { month: "Apr", members: 34 },
  { month: "May", members: 35 },
]

const config = {
  members: {
    label: "Active members",
    color: "var(--chart-1)",
  },
} satisfies ChartConfig

export function MemberGrowthAreaChart() {
  return (
    <ChartContainer
      config={config}
      className="aspect-16/5 min-h-50 w-full figures"
    >
      <AreaChart accessibilityLayer data={data} margin={{ left: 0, right: 0, top: 12, bottom: 0 }}>
        <defs>
          <linearGradient id="memberGrowthFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--color-members)" stopOpacity={0.45} />
            <stop offset="100%" stopColor="var(--color-members)" stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke="var(--rule)" strokeDasharray="2 6" />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={{ stroke: "var(--rule)" }}
          tickMargin={12}
          tick={{ fill: "var(--muted-foreground)", fontSize: 11, letterSpacing: "0.08em", fontWeight: 600, style: { textTransform: "uppercase" } }}
        />
        <ChartTooltip content={<ChartTooltipContent indicator="line" />} />
        <Area
          dataKey="members"
          type="monotone"
          stroke="var(--color-members)"
          strokeWidth={2.25}
          fill="url(#memberGrowthFill)"
        />
      </AreaChart>
    </ChartContainer>
  )
}

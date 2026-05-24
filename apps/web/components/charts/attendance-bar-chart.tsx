"use client"

import { Bar, BarChart, CartesianGrid, XAxis } from "recharts"

import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const data = [
  { week: "Mar 9", members: 22 },
  { week: "Mar 16", members: 28 },
  { week: "Mar 23", members: 25 },
  { week: "Mar 30", members: 31 },
  { week: "Apr 6", members: 27 },
  { week: "Apr 13", members: 33 },
  { week: "Apr 20", members: 30 },
  { week: "Apr 27", members: 29 },
  { week: "May 4", members: 34 },
  { week: "May 11", members: 32 },
  { week: "May 18", members: 35 },
  { week: "May 25", members: 31 },
]

const config = {
  members: {
    label: "Members present",
    color: "var(--chart-1)",
  },
} satisfies ChartConfig

export function AttendanceBarChart() {
  return (
    <ChartContainer
      config={config}
      className="aspect-16/6 min-h-50 w-full figures"
    >
      <BarChart accessibilityLayer data={data} margin={{ left: 0, right: 0, top: 12, bottom: 0 }}>
        <CartesianGrid
          vertical={false}
          stroke="var(--rule)"
          strokeDasharray="2 6"
        />
        <XAxis
          dataKey="week"
          tickLine={false}
          axisLine={{ stroke: "var(--rule)" }}
          tickMargin={12}
          tick={{ fill: "var(--muted-foreground)", fontSize: 11, letterSpacing: "0.08em", fontWeight: 600, textTransform: "uppercase" }}
        />
        <ChartTooltip
          cursor={{ fill: "var(--primary)", opacity: 0.12 }}
          content={<ChartTooltipContent indicator="line" hideIndicator />}
        />
        <Bar dataKey="members" fill="var(--color-members)" radius={[3, 3, 0, 0]} />
      </BarChart>
    </ChartContainer>
  )
}

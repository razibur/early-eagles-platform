"use client"

import { CartesianGrid, Line, LineChart, XAxis } from "recharts"

import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const data = [
  { month: "Dec", zelle: 220, etransfer: 90, cash: 45 },
  { month: "Jan", zelle: 285, etransfer: 110, cash: 30 },
  { month: "Feb", zelle: 310, etransfer: 95, cash: 50 },
  { month: "Mar", zelle: 345, etransfer: 120, cash: 40 },
  { month: "Apr", zelle: 380, etransfer: 130, cash: 35 },
  { month: "May", zelle: 405, etransfer: 140, cash: 30 },
]

const config = {
  zelle: { label: "Zelle", color: "var(--chart-1)" },
  etransfer: { label: "e-Transfer", color: "var(--chart-2)" },
  cash: { label: "Cash", color: "var(--chart-3)" },
} satisfies ChartConfig

export function DuesLineChart() {
  return (
    <ChartContainer
      config={config}
      className="aspect-video min-h-60 w-full figures"
    >
      <LineChart accessibilityLayer data={data} margin={{ left: 0, right: 4, top: 16, bottom: 0 }}>
        <CartesianGrid vertical={false} stroke="var(--rule)" strokeDasharray="2 6" />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={{ stroke: "var(--rule)" }}
          tickMargin={12}
          tick={{ fill: "var(--muted-foreground)", fontSize: 11, letterSpacing: "0.08em", fontWeight: 600, textTransform: "uppercase" }}
        />
        <ChartTooltip
          cursor={{ stroke: "var(--primary)", strokeDasharray: "2 4", strokeWidth: 1 }}
          content={<ChartTooltipContent indicator="dot" />}
        />
        <Line
          dataKey="zelle"
          type="monotone"
          stroke="var(--color-zelle)"
          strokeWidth={2.25}
          dot={{ r: 3, fill: "var(--color-zelle)", strokeWidth: 0 }}
          activeDot={{ r: 5, strokeWidth: 0 }}
        />
        <Line
          dataKey="etransfer"
          type="monotone"
          stroke="var(--color-etransfer)"
          strokeWidth={1.75}
          strokeDasharray="5 4"
          dot={false}
        />
        <Line
          dataKey="cash"
          type="monotone"
          stroke="var(--color-cash)"
          strokeWidth={1.75}
          strokeDasharray="1 4"
          dot={false}
        />
        <ChartLegend content={<ChartLegendContent />} />
      </LineChart>
    </ChartContainer>
  )
}

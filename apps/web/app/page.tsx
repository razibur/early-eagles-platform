import { Masthead } from "@/components/editorial/masthead"
import { SectionHeader } from "@/components/editorial/section-header"
import { StatFigure } from "@/components/editorial/stat-figure"
import { Rule } from "@/components/editorial/rule"
import { RevealStack, RevealItem } from "@/components/editorial/reveal"
import { AttendanceBarChart } from "@/components/charts/attendance-bar-chart"
import { DuesLineChart } from "@/components/charts/dues-line-chart"
import { MemberGrowthAreaChart } from "@/components/charts/member-growth-area-chart"

export default function DashboardPage() {
  return (
    <main className="mx-auto w-full max-w-6xl px-5 md:px-8 lg:px-12 py-8 md:py-12">
      <RevealStack className="flex flex-col gap-12 md:gap-16">
        <RevealItem as="header">
          <Masthead edition="Match Day · 24.05.26" />
        </RevealItem>

        {/* Hero strip — the week at a glance */}
        <RevealItem as="section" className="grid grid-cols-1 md:grid-cols-12 gap-6 md:gap-8">
          <div className="md:col-span-7">
            <SectionHeader
              kicker="This Sunday"
              title="Court time, dues, and a quietly growing roster."
              meta="24 · 05 · 26"
            />
          </div>
          <div className="md:col-span-5 md:pl-8 md:border-l md:border-rule flex">
            <p className="text-base leading-relaxed text-muted-foreground self-end">
              Thirty-one members took the court last Sunday — three regulars
              travelling, one injured. Dues held the line and the roster ticked
              up again. Quick look below.
            </p>
          </div>
        </RevealItem>

        {/* Featured stats — three jersey-numeral cards with primary emphasis on the lead */}
        <RevealItem as="section">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 md:gap-6">
            <StatFigure
              label="Members · This Sunday"
              value="31"
              unit="of 35"
              delta={{ value: "−4", direction: "down" }}
              caption="Three regulars travelling, one injured."
              emphasis="primary"
            />
            <StatFigure
              label="Dues · May"
              value="$575"
              delta={{ value: "+4.2%", direction: "up" }}
              caption="Zelle still the workhorse; cash steady."
            />
            <StatFigure
              label="Active Roster"
              value="35"
              delta={{ value: "+1", direction: "up" }}
              caption="One new sign-up via referral."
            />
          </div>
        </RevealItem>

        {/* Attendance — full-width feature chart */}
        <RevealItem as="section" className="grid grid-cols-1 md:grid-cols-12 gap-6 md:gap-8">
          <div className="md:col-span-12">
            <SectionHeader
              kicker="Feature · Court Attendance"
              title="Twelve Sundays, by the head-count."
              meta="Mar 09 → May 25"
            />
          </div>
          <div className="md:col-span-12 mt-2 rounded-lg bg-card/40 ring-1 ring-rule p-4 md:p-6">
            <AttendanceBarChart />
            <Rule className="mt-6" tone="default" />
            <p className="mt-4 text-sm leading-snug text-muted-foreground max-w-prose">
              The peak — 35 — sits cleanly inside the spring stretch. The lower
              weeks track Eid and a single holiday gym closure. No structural
              decline.
            </p>
          </div>
        </RevealItem>

        {/* Dues — 7/5 split, chart left, pull-quote right */}
        <RevealItem as="section" className="grid grid-cols-1 md:grid-cols-12 gap-6 md:gap-8">
          <div className="md:col-span-7 rounded-lg bg-card/40 ring-1 ring-rule p-4 md:p-6">
            <SectionHeader
              kicker="Ledger · Monthly Dues"
              title="Three rivers, one tide."
              meta="Six months · USD"
            />
            <div className="mt-6">
              <DuesLineChart />
            </div>
          </div>
          <aside className="md:col-span-5 md:pl-8 md:border-l md:border-rule flex flex-col gap-5 justify-end pb-2">
            <span className="label label-accent">Treasurer&apos;s Note</span>
            <blockquote className="display text-2xl md:text-3xl leading-[1.1] text-foreground">
              &ldquo;Zelle keeps doing the heavy lifting. e-Transfer is creeping
              up quietly. Cash will be cash — and that&apos;s fine.&rdquo;
            </blockquote>
            <p className="label">— Tenant 0, Treasury</p>
          </aside>
        </RevealItem>

        {/* Member growth — full-width area */}
        <RevealItem as="section" className="grid grid-cols-1 md:grid-cols-12 gap-6 md:gap-8">
          <div className="md:col-span-12">
            <SectionHeader
              kicker="Long View · Roster"
              title="Slow growth is still growth."
              meta="Nov → May"
            />
          </div>
          <div className="md:col-span-12 mt-2 rounded-lg bg-card/40 ring-1 ring-rule p-4 md:p-6">
            <MemberGrowthAreaChart />
          </div>
        </RevealItem>

        {/* Footer band — quick stats + colophon */}
        <RevealItem as="section">
          <Rule weight="bold" tone="primary" className="mb-6" />
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-8">
            <div>
              <span className="label">Total members</span>
              <p className="display figures text-3xl text-foreground mt-1">35</p>
            </div>
            <div>
              <span className="label">Weekly avg</span>
              <p className="display figures text-3xl text-foreground mt-1">29.3</p>
            </div>
            <div>
              <span className="label">Dues YTD</span>
              <p className="display figures text-3xl text-foreground mt-1">$1,945</p>
            </div>
            <div>
              <span className="label">Streak</span>
              <p className="display figures text-3xl text-primary mt-1">24 wk</p>
            </div>
          </div>
          <div className="flex flex-col md:flex-row md:items-baseline md:justify-between gap-3 text-xs uppercase tracking-[0.2em] text-muted-foreground border-t border-rule pt-6">
            <span>Tenant 0 · Sunday Edition</span>
            <span className="text-center">Set in Big Shoulders &amp; Hanken Grotesk</span>
            <span className="md:text-right">Next match · 31 · 05 · 26</span>
          </div>
        </RevealItem>
      </RevealStack>
    </main>
  )
}

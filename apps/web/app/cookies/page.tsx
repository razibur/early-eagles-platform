import type { Metadata } from "next"

import { CookieSettingsButton } from "@/components/consent/cookie-settings-button"

export const metadata: Metadata = { title: "Cookie policy — Early Eagles" }

// The published cookie policy (P0-076.2). The maintained inventory and the
// review notes live in docs/cookie-policy.md; keep the two tables in step.
const necessary = [
  {
    name: "ee_consent",
    where: "Cookie",
    lifetime: "1 year",
    purpose: "Remembers your cookie choice and when you made it.",
  },
  {
    name: "ee-theme",
    where: "Local storage",
    lifetime: "Until you clear it",
    purpose: "Remembers light, dark or system display.",
  },
]

export default function CookiePolicyPage() {
  return (
    <main className="mx-auto w-full max-w-3xl flex-1 px-5 py-12 md:px-8 md:py-16">
      <span className="label label-accent text-primary">Policy · Updated 27 Oct 2026</span>
      <h1 className="display mt-6 text-5xl text-foreground md:text-7xl">Cookies</h1>

      <div className="mt-8 space-y-5 text-base leading-relaxed text-muted-foreground">
        <p>
          Early Eagles keeps only what it needs to work, plus, if you allow it, anonymous counts of which pages
          get used. We don&apos;t run ads, and we don&apos;t sell or share what we collect.
        </p>
        <p>
          <strong className="text-foreground">Necessary</strong> storage is always on, because without it the app
          can&apos;t remember your choices. <strong className="text-foreground">Analytics</strong> is off until you
          turn it on, and you can turn it off again at any time.
        </p>
      </div>

      <h2 className="label mt-12 text-foreground">Necessary · always on</h2>
      <div className="mt-4 overflow-x-auto rounded-lg ring-1 ring-rule">
        <table className="w-full text-left text-sm">
          <thead className="bg-foreground/5 text-[0.6875rem] font-bold uppercase tracking-[0.16em] text-muted-foreground">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Where</th>
              <th className="px-4 py-3">Kept for</th>
              <th className="px-4 py-3">Why</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-rule">
            {necessary.map((row) => (
              <tr key={row.name}>
                <td className="px-4 py-3 font-mono text-xs text-foreground">{row.name}</td>
                <td className="px-4 py-3 text-muted-foreground">{row.where}</td>
                <td className="px-4 py-3 text-muted-foreground">{row.lifetime}</td>
                <td className="px-4 py-3 text-muted-foreground">{row.purpose}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2 className="label mt-12 text-foreground">Analytics · only with your OK</h2>
      <p className="mt-4 text-base leading-relaxed text-muted-foreground">
        None in use yet. When page-visit analytics is added, it will load only if you&apos;ve allowed it, and it
        will be listed here first.
      </p>

      <div className="mt-12 flex flex-wrap items-center gap-4 border-t border-rule pt-8">
        <CookieSettingsButton />
        <span className="text-sm text-muted-foreground">
          Questions: <a href="mailto:moh.razib@gmail.com" className="text-foreground underline underline-offset-4">moh.razib@gmail.com</a>
        </span>
      </div>
    </main>
  )
}

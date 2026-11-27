import { Masthead } from "@/components/editorial/masthead"
import { RevealStack, RevealItem } from "@/components/editorial/reveal"
import { ThisSundayCard } from "@/components/home/this-sunday-card"

// The member home (ADR-0004): the next session first. The sample charts that
// used to follow moved to /insights (P1-034); P1-032 fills the rest.
export default function HomePage() {
  return (
    <main className="mx-auto w-full max-w-6xl px-5 md:px-8 lg:px-12 py-8 md:py-12">
      <RevealStack className="flex flex-col gap-12 md:gap-16">
        <RevealItem as="header">
          <Masthead edition="Match Day · 24.05.26" />
        </RevealItem>

        {/* P1-014: the next session, your answer and your dues */}
        <RevealItem as="section">
          <ThisSundayCard />
        </RevealItem>
      </RevealStack>
    </main>
  )
}

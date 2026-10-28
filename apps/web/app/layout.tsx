import type { Metadata, Viewport } from "next"
import { Big_Shoulders, Hanken_Grotesk } from "next/font/google"
import { ConsentHydrator } from "@/components/providers/consent-hydrator"
import { QueryProvider } from "@/components/providers/query-provider"
import { ServiceWorkerRegistration } from "@/components/pwa/service-worker-registration"
import { AppTopBar } from "@/components/shell/app-top-bar"
import { BottomNav } from "@/components/shell/bottom-nav"
import { themeBootScript } from "@/lib/theme"
import "./globals.css"

const bigShoulders = Big_Shoulders({
  variable: "--font-display",
  subsets: ["latin"],
  weight: "variable",
  axes: ["opsz"],
  display: "swap",
})

const hanken = Hanken_Grotesk({
  variable: "--font-sans",
  subsets: ["latin"],
  display: "swap",
})

export const metadata: Metadata = {
  title: "Early Eagles — Club HQ",
  description:
    "Live club dashboard — attendance, dues, members, court time. Sunday rituals, kept honest.",
  // iOS ignores most of the manifest; this sets the home-screen title and a
  // dark status bar. Not "black-translucent": that draws the page under the
  // status bar, and nothing pads for env(safe-area-inset-top) yet.
  appleWebApp: {
    title: "Early Eagles",
    statusBarStyle: "black",
  },
}

// Browser chrome color; matches the manifest's theme_color.
export const viewport: Viewport = {
  themeColor: "#080d11",
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    // suppressHydrationWarning: the boot script may add `.light` and a
    // color-scheme style before React hydrates (P0-070.3).
    <html
      lang="en"
      className={`${bigShoulders.variable} ${hanken.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeBootScript }} />
      </head>
      {/* Bottom padding on phones = BottomNav height (4rem of tabs + 1px border + safe area), so the page end stays clear of it. */}
      <body className="min-h-full flex flex-col bg-background text-foreground font-sans selection:bg-primary/30 selection:text-foreground pb-[calc(4rem+1px+env(safe-area-inset-bottom))] md:pb-0">
        <QueryProvider>
          <AppTopBar />
          {children}
          <BottomNav />
        </QueryProvider>
        <ConsentHydrator />
        <ServiceWorkerRegistration />
      </body>
    </html>
  )
}

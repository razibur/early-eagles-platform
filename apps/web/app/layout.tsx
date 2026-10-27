import type { Metadata, Viewport } from "next"
import { Big_Shoulders, Hanken_Grotesk } from "next/font/google"
import { ServiceWorkerRegistration } from "@/components/pwa/service-worker-registration"
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
    <html
      lang="en"
      className={`${bigShoulders.variable} ${hanken.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col bg-background text-foreground font-sans selection:bg-primary/30 selection:text-foreground">
        {children}
        <ServiceWorkerRegistration />
      </body>
    </html>
  )
}

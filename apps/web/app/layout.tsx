import type { Metadata, Viewport } from "next"
import { headers } from "next/headers"
import { ClerkProvider } from "@clerk/nextjs"
import { Big_Shoulders, Hanken_Grotesk } from "next/font/google"
import { AnonymousApiProvider, ClerkApiProvider } from "@/components/providers/api-provider"
import { ConsentHydrator } from "@/components/providers/consent-hydrator"
import { QueryProvider } from "@/components/providers/query-provider"
import { TenantProvider } from "@/components/providers/tenant-provider"
import { InstallCard } from "@/components/pwa/install-card"
import { ServiceWorkerRegistration } from "@/components/pwa/service-worker-registration"
import { AppTopBar } from "@/components/shell/app-top-bar"
import { BottomNav } from "@/components/shell/bottom-nav"
import { SiteFooter } from "@/components/shell/site-footer"
import { CookieBanner } from "@/components/consent/cookie-banner"
import { clerkAppearance } from "@/lib/auth/appearance"
import { authMode } from "@/lib/auth/config"
import { AFTER_AUTH_PATH, SIGN_IN_PATH, SIGN_UP_PATH } from "@/lib/auth/routes"
import { TENANT_SLUG_HEADER } from "@/lib/tenancy/subdomain"
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

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  // The slug proxy.ts resolved from the subdomain (P0-077.2). Reading request
  // headers makes every page render per request rather than at build time;
  // that's expected for a multi-tenant app whose pages are per tenant.
  const tenantSlug = (await headers()).get(TENANT_SLUG_HEADER)
  // Throws when the Clerk key is missing (P0-039.1): no page renders rather
  // than every page rendering without sign-in.
  const auth = authMode()

  const Api = auth === "clerk" ? ClerkApiProvider : AnonymousApiProvider
  const app = (
    <TenantProvider slug={tenantSlug}>
      <QueryProvider>
        {/* The API client: with the Clerk session token, or none without Clerk (P0-071.2). */}
        <Api>
          <AppTopBar />
          {children}
          <SiteFooter />
          <BottomNav />
        </Api>
      </QueryProvider>
    </TenantProvider>
  )

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
      <body className="min-h-full flex flex-col bg-background text-foreground font-sans selection:bg-primary/30 selection:text-foreground pb-[calc(4rem+1px+env(safe-area-inset-bottom)+var(--floating-card-space,0px))] md:pb-[var(--floating-card-space,0px)]">
        {/* Clerk's session context for everything below (P0-039.2). Off only in
            builds that opted out with EE_AUTH_DISABLED=1 (CI, Playwright). */}
        {auth === "clerk" ? (
          <ClerkProvider
            appearance={clerkAppearance}
            signInUrl={SIGN_IN_PATH}
            signUpUrl={SIGN_UP_PATH}
            signInFallbackRedirectUrl={AFTER_AUTH_PATH}
            signUpFallbackRedirectUrl={AFTER_AUTH_PATH}
          >
            {app}
          </ClerkProvider>
        ) : (
          app
        )}
        <ConsentHydrator />
        <CookieBanner />
        <InstallCard />
        <ServiceWorkerRegistration />
      </body>
    </html>
  )
}

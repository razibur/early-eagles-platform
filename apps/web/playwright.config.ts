import { defineConfig, devices } from "@playwright/test"

import { E2E_PROXY_KEY, ECHO_API_PORT } from "./e2e/fixtures/echo-api-settings"

// Browser tests for the web app (P0-070.4). Run against a production build
// (`next build` + `next start`), not the dev server: dev adds its own overlay
// and indicator to the page and compiles routes on first hit.
//
//   npm --workspace web run test:e2e
//
// phone / tablet / desktop use the installed Google Chrome (`channel: "chrome"`),
// so they need no browser download. The iphone project runs in Playwright's
// WebKit, the engine behind every iPhone browser (P0-070.5); install it once
// with `npx playwright install webkit` (CI does this in web.yml).
const port = 3200
const iphone = { ...devices["iPhone 15"], serviceWorkers: "block" as const }
const RETURNING_VISITOR_STATE = "e2e/.state/returning-iphone.json"

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: `http://localhost:${port}`,
    trace: "retain-on-failure",
  },
  // The three layouts the app is designed for, in Chrome (as on Android and
  // desktop). 768 is the md breakpoint, the first width that gets the desktop
  // navigation. Then the phone layout again in WebKit with an iPhone's screen,
  // touch and user agent, because members on iPhones get Safari's engine.
  // WebKit only: service workers are blocked. Once public/sw.js controls the
  // page, WebKit stops showing the page's later fetches to page.route, so the
  // specs' API stubs were skipped and the echo API answered instead (the page
  // then failed on a session with no date). Real iPhones are unaffected: the
  // worker has no fetch handler.
  // iphone specs also start as a returning visitor (iphone-setup): on that
  // short screen the cookie banner and install card cover too much of the page
  // for Playwright's auto-scroll. The card specs opt out and start fresh.
  projects: [
    { name: "phone", use: { channel: "chrome", viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, hasTouch: true } },
    { name: "tablet", use: { channel: "chrome", viewport: { width: 768, height: 1024 }, deviceScaleFactor: 2, hasTouch: true } },
    { name: "desktop", use: { channel: "chrome", viewport: { width: 1280, height: 800 } } },
    { name: "iphone-setup", testMatch: /returning-visitor\.setup\.ts/, use: { ...iphone } },
    { name: "iphone", dependencies: ["iphone-setup"], use: { ...iphone, storageState: RETURNING_VISITOR_STATE } },
  ],
  webServer: [
    {
      command: `npm run build && npx next start --port ${port}`,
      url: `http://localhost:${port}`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      // No Clerk instance here (P0-039.1): run the build without sign-in, even
      // when apps/web/.env.local holds real keys. Process env beats .env files.
      // /api/* goes to the echo API below instead of a real one (P0-071.5).
      env: {
        EE_AUTH_DISABLED: "1",
        EE_API_ORIGIN: `http://127.0.0.1:${ECHO_API_PORT}`,
        EE_API_PROXY_KEY: E2E_PROXY_KEY,
      },
    },
    {
      // Stand-in API that echoes what it receives (e2e/fixtures/echo-api.mjs).
      command: `node e2e/fixtures/echo-api.mjs ${ECHO_API_PORT}`,
      url: `http://127.0.0.1:${ECHO_API_PORT}/__ready`,
      reuseExistingServer: !process.env.CI,
    },
  ],
})

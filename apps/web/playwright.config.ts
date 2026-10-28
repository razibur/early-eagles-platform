import { defineConfig } from "@playwright/test"

// Browser tests for the web app (P0-070.4). Run against a production build
// (`next build` + `next start`), not the dev server: dev adds its own overlay
// and indicator to the page and compiles routes on first hit.
//
//   npm --workspace web run test:e2e
//
// Uses the installed Google Chrome (`channel: "chrome"`), so no browser
// download is needed locally or on GitHub's ubuntu runners, which ship Chrome.
const port = 3200

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: `http://localhost:${port}`,
    channel: "chrome",
    trace: "retain-on-failure",
  },
  // The three layouts the app is designed for. 768 is the md breakpoint, the
  // first width that gets the desktop navigation.
  projects: [
    { name: "phone", use: { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, hasTouch: true } },
    { name: "tablet", use: { viewport: { width: 768, height: 1024 }, deviceScaleFactor: 2, hasTouch: true } },
    { name: "desktop", use: { viewport: { width: 1280, height: 800 } } },
  ],
  webServer: {
    command: `npm run build && npx next start --port ${port}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
})

// Shared by playwright.config.ts and the forwarding tests (P0-071.5).
export const ECHO_API_PORT = 3299

// Test-only value for the proxy key the web app sends the echo API.
export const E2E_PROXY_KEY = "e2e-only-proxy-key-not-a-secret-000000"

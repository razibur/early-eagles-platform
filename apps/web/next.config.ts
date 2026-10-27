import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The service worker (public/sw.js, ADR-0011) must never be served from a
  // cache, or a deploy would leave browsers running the old worker. The CSP
  // keeps it to same-origin scripts.
  async headers() {
    return [
      {
        source: "/sw.js",
        headers: [
          { key: "Content-Type", value: "application/javascript; charset=utf-8" },
          { key: "Cache-Control", value: "no-cache, no-store, must-revalidate" },
          { key: "Content-Security-Policy", value: "default-src 'self'; script-src 'self'" },
        ],
      },
    ];
  },
};

export default nextConfig;

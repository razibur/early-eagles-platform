import type { MetadataRoute } from "next"

// Web app manifest (P0-067.2, ADR-0011), served at /manifest.webmanifest
// and linked from every page by Next. Icons come from
// scripts/generate-icons.mjs; colors are the dark theme's --background.
//
// One manifest for every tenant for now. Per-club names and icons are a
// tenant-branding concern for later, when a second club exists.
const background = "#080d11"

export default function manifest(): MetadataRoute.Manifest {
  return {
    id: "/",
    name: "Early Eagles — Club HQ",
    short_name: "Early Eagles",
    description:
      "Live club dashboard — attendance, dues, members, court time.",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: background,
    theme_color: background,
    categories: ["sports"],
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  }
}

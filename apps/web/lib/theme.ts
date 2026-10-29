// Theme preference (P0-070.3). Dark is the default surface; `.light` on
// <html> switches every token in globals.css. "system" follows the OS.
export type Theme = "light" | "dark" | "system"

export const THEME_STORAGE_KEY = "ee-theme"

// Browser chrome colors: the two themes' --background, as hex.
export const THEME_COLORS = { dark: "#080d11", light: "#f6f9fb" } as const

export function readTheme(): Theme {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return value === "light" || value === "dark" || value === "system" ? value : "dark"
  } catch {
    // Storage blocked (private mode, policy): fall back to the default.
    return "dark"
  }
}

export function writeTheme(theme: Theme) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Not persisted; the choice still applies for this page view.
  }
}

export function resolveTheme(theme: Theme): "light" | "dark" {
  if (theme !== "system") return theme
  return window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark"
}

export function applyTheme(theme: Theme) {
  const resolved = resolveTheme(theme)
  document.documentElement.classList.toggle("light", resolved === "light")
  document.documentElement.style.colorScheme = resolved
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", THEME_COLORS[resolved])
}

// Runs in <head> before first paint so a light-theme user never sees a dark
// frame. Self-contained (no imports reach it) and wrapped in try/catch: it
// must never break the page.
//
// A plain literal on purpose: no values are interpolated into code that goes
// into a <script> tag (CodeQL js/bad-code-sanitization). Keep "ee-theme" in
// step with THEME_STORAGE_KEY and "#f6f9fb" with THEME_COLORS.light; the
// "theme choice survives a reload" e2e test fails if the key drifts.
export const themeBootScript = `(function(){try{var t=localStorage.getItem("ee-theme");var l=t==="light"||(t==="system"&&matchMedia("(prefers-color-scheme: light)").matches);var d=document.documentElement;if(l){d.classList.add("light")}d.style.colorScheme=l?"light":"dark";if(l){document.addEventListener("DOMContentLoaded",function(){var m=document.querySelector('meta[name="theme-color"]');if(m){m.setAttribute("content","#f6f9fb")}})}}catch(e){}})()`

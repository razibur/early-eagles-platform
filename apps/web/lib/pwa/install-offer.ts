// The install offer behind the PWA card (P1-023): whether this browser can
// install the app, and whether this device has already been asked.
//
// Chromium fires `beforeinstallprompt`, which we hold on to and replay from the
// card's button. iOS has no such event and no way to install from script, so
// the card explains the Share menu instead.
//
// The listeners attach when this module loads, not in an effect: the event can
// fire before React hydrates, and it isn't sent twice.
export const INSTALL_CARD_STORAGE_KEY = "ee-install-card"

export type InstallOffer = "prompt" | "ios"

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<unknown>
}

let deferredPrompt: BeforeInstallPromptEvent | null = null
// This page view took the device's one showing, so keep the card up until it's answered.
let claimed = false
let closed = false

const listeners = new Set<() => void>()

function notify() {
  listeners.forEach((listener) => listener())
}

if (typeof window !== "undefined") {
  window.addEventListener("beforeinstallprompt", (event) => {
    // Stops Chrome's own install bar, so the card is the only ask.
    event.preventDefault()
    deferredPrompt = event as BeforeInstallPromptEvent
    notify()
  })

  // Also covers an install from the browser menu while the card is up.
  window.addEventListener("appinstalled", () => {
    deferredPrompt = null
    closed = true
    notify()
  })
}

function isStandalone() {
  const iosStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true
  return iosStandalone || window.matchMedia("(display-mode: standalone)").matches
}

function isIos() {
  const { userAgent, maxTouchPoints } = navigator
  // iPadOS Safari reports itself as a Mac; only the touch points give it away.
  return /iPad|iPhone|iPod/.test(userAgent) || (/Macintosh/.test(userAgent) && maxTouchPoints > 1)
}

function hasBeenShown() {
  try {
    return localStorage.getItem(INSTALL_CARD_STORAGE_KEY) !== null
  } catch {
    // Storage blocked (private mode, policy): nothing to remember it by.
    return false
  }
}

export function subscribeInstallOffer(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function readInstallOffer(): InstallOffer | null {
  if (closed || isStandalone()) return null
  if (!claimed && hasBeenShown()) return null
  if (deferredPrompt) return "prompt"
  return isIos() ? "ios" : null
}

// Called when the card goes on screen, not when it's answered: the plan asks for
// once per device, so a card that's ignored doesn't come back on the next visit.
export function claimInstallCard() {
  claimed = true
  try {
    localStorage.setItem(INSTALL_CARD_STORAGE_KEY, new Date().toISOString())
  } catch {
    // Not persisted; it will show again on the next page load.
  }
}

export function closeInstallCard() {
  closed = true
  notify()
}

export function acceptInstall() {
  const prompt = deferredPrompt
  deferredPrompt = null
  closeInstallCard()
  // A prompt can only be used once, and only from a click, so this is its one shot.
  prompt?.prompt().catch((error: unknown) => {
    console.error("Install prompt failed", error)
  })
}

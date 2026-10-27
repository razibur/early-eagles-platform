"use client"

import { useEffect, useSyncExternalStore } from "react"
import { Monitor, Moon, Sun } from "lucide-react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { THEME_STORAGE_KEY, applyTheme, readTheme, writeTheme, type Theme } from "@/lib/theme"

const options: { value: Theme; label: string; icon: typeof Sun }[] = [
  { value: "light", label: "Light", icon: Sun },
  { value: "dark", label: "Dark", icon: Moon },
  { value: "system", label: "System", icon: Monitor },
]

const THEME_CHANGE_EVENT = "ee-theme-change"

// Re-render on our own changes and on another tab's (the storage event),
// re-applying the theme so open tabs stay in step.
function subscribe(onChange: () => void) {
  const onStorage = (event: StorageEvent) => {
    if (event.key !== THEME_STORAGE_KEY) return
    applyTheme(readTheme())
    onChange()
  }
  window.addEventListener(THEME_CHANGE_EVENT, onChange)
  window.addEventListener("storage", onStorage)
  return () => {
    window.removeEventListener(THEME_CHANGE_EVENT, onChange)
    window.removeEventListener("storage", onStorage)
  }
}

// Light / dark / system (P0-070.3). The boot script in the root layout has
// already applied the saved choice before paint; this component only reads
// it back, changes it, and follows the OS while "system" is selected.
export function ThemeSwitcher() {
  // The saved choice is an external store (localStorage). The server
  // snapshot is null, so the first render matches the server HTML and the
  // real value arrives right after hydration.
  const theme = useSyncExternalStore(subscribe, readTheme, () => null)

  useEffect(() => {
    if (theme !== "system") return
    const query = window.matchMedia("(prefers-color-scheme: light)")
    const follow = () => applyTheme("system")
    query.addEventListener("change", follow)
    return () => query.removeEventListener("change", follow)
  }, [theme])

  function choose(value: Theme) {
    writeTheme(value)
    applyTheme(value)
    window.dispatchEvent(new Event(THEME_CHANGE_EVENT))
  }

  const Current = options.find((o) => o.value === theme)?.icon ?? Moon

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Theme"
        className="group inline-flex size-9 items-center justify-center rounded-full outline-none transition-colors hover:bg-foreground/5 focus-visible:ring-2 focus-visible:ring-ring data-[popup-open]:bg-foreground/5"
      >
        <Current
          className="size-4 text-muted-foreground transition-[color,transform] duration-300 group-hover:rotate-12 group-hover:text-foreground"
          aria-hidden
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" sideOffset={8} className="min-w-40">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <span className="label">Theme</span>
          </DropdownMenuLabel>
          <DropdownMenuRadioGroup value={theme ?? undefined} onValueChange={(value) => choose(value as Theme)}>
            {options.map(({ value, label, icon: Icon }) => (
              <DropdownMenuRadioItem key={value} value={value}>
                <Icon aria-hidden />
                {label}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

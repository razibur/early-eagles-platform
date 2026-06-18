# EarlyEagles design system

The locked aesthetic, the colour and type tokens, the motion principles, and the do-and-don't list for every future surface. This document is the single source of truth for visual decisions — when in doubt, this wins over personal taste.

**Last updated:** 2026-05-28
**Locked direction:** Athletic-modern, dark-by-default. See [ADR-0004](../adr/0004-role-based-landing-and-routing.md) for routing context and [`feedback_frontend_design`](file:///c:/Users/skyli/.claude/projects/d--Projects-EarlyEagles/memory/feedback_frontend_design.md) memory for why this direction was chosen.

## 1. Aesthetic in one paragraph

EarlyEagles looks like a modern sports-team app — IPL match dashboard meets Bloomberg terminal — rooted in Bangladeshi cultural identity. **Court-black** ground, **Bangladesh-green** as the primary action color, **Bangladesh-red** as the live / destructive signal, **steel-white** type. Display typography is heavy, narrow, and athletic (jersey-numeral energy). Body type is a clean grotesque. Motion is purposeful — entrance reveals, live indicators, chart draw-ins — never decorative. Information is dense but the hierarchy is loud. The aesthetic is **not** vintage editorial / almanac / serif — that direction was prototyped on 2026-05-24 and abandoned the same day after the team logo was revealed.

## 2. Color tokens

All color tokens live in [`apps/web/app/globals.css`](../../apps/web/app/globals.css). Default theme is dark; `.light` is the opt-in light variant. **Never hard-code colors in TSX.** Always reference the semantic token name via Tailwind classes (`bg-primary`, `text-foreground`) or CSS variables (`var(--primary)`).

### 2.1 Semantic palette — dark default

| Token | OKLCH value | Reads as | Used for |
|---|---|---|---|
| `--background` | `oklch(0.155 0.012 240)` | Court black, slight cool cast | App background |
| `--foreground` | `oklch(0.97 0.003 240)` | Steel white | Body text, primary content |
| `--card` | `oklch(0.205 0.013 240)` | One step lift from background | Cards, panels |
| `--paper` | `oklch(0.18 0.012 240)` | Subtle surface | Section backgrounds |
| `--paper-edge` | `oklch(0.24 0.014 240)` | Slightly lighter | Card edges, dividers |
| `--rule` | `oklch(0.3 0.014 240)` | Hairline separator | Borders, grid lines, axis lines |
| `--muted` | `oklch(0.23 0.013 240)` | Background-adjacent | Disabled / inactive areas |
| `--muted-foreground` | `oklch(0.68 0.012 240)` | Steel grey | Captions, labels, axis ticks |
| `--primary` | `oklch(0.6 0.135 168)` | Bangladesh green | Primary actions, links, success, focus rings |
| `--primary-foreground` | `oklch(0.12 0.01 240)` | Near-black | Text on primary background |
| `--destructive` | `oklch(0.62 0.22 25)` | Bangladesh red | Destructive actions, errors |
| `--live` | `oklch(0.66 0.225 25)` | Live red | Pulsing live indicators |
| `--shuttle` | `oklch(0.85 0.18 95)` | Shuttle-cork yellow | Sparingly: highlight, badge accents |
| `--accent` | `oklch(0.26 0.015 240)` | Subtle lift | Hover backgrounds |
| `--secondary` | `oklch(0.25 0.014 240)` | Card-adjacent | Secondary surfaces |

### 2.2 Chart palette — tonal, intentional

| Token | OKLCH | Color | Use |
|---|---|---|---|
| `--chart-1` | `oklch(0.62 0.14 168)` | Bangladesh green | **Primary series.** First / most-important data. |
| `--chart-2` | `oklch(0.85 0.005 240)` | Steel white | **Secondary series.** High contrast against green. |
| `--chart-3` | `oklch(0.66 0.225 25)` | Bangladesh red | **Alert / exception.** Use for warnings, not as a default 3rd series. |
| `--chart-4` | `oklch(0.68 0.11 220)` | Cyan-steel | Cool fourth color (references court mat blue). |
| `--chart-5` | `oklch(0.85 0.18 95)` | Shuttle yellow | **Sparingly.** Final color when you need one more — never as a primary. |

### 2.3 Light mode

Toggled by adding `.light` class to `<html>` or any parent. **Currently not wired to a theme switcher** — that's [`P0-070.3`](../../PROGRESS.md). All tokens have light-mode equivalents in `globals.css`. Do not test light-mode-only — every component must work in dark and light.

### 2.4 Color rules

- **Do** use `bg-primary`, `text-foreground`, `border-rule`, etc. Tailwind class names map to the tokens via `@theme inline`.
- **Do** use `var(--chart-N)` directly in Recharts `stroke` / `fill` props — see [`apps/web/components/charts/attendance-bar-chart.tsx`](../../apps/web/components/charts/attendance-bar-chart.tsx).
- **Don't** hard-code hex / rgb / hsl. Don't use `#fff`, `#000`, `red`, etc.
- **Don't** introduce new colors without adding them to `globals.css` first. If you need a new accent, add a token, don't inline an oklch literal in TSX.
- **Don't** use shuttle-yellow (`--shuttle`, `--chart-5`) as a primary action color. It loses meaning if overused.
- **Don't** use destructive red (`--destructive`) for anything other than errors / destructive confirmations / live alerts.

## 3. Typography

Two fonts. No more. Loaded via `next/font/google` in [`apps/web/app/layout.tsx`](../../apps/web/app/layout.tsx).

### 3.1 Font families

| Font | CSS variable | Tailwind | Use |
|---|---|---|---|
| **Big Shoulders** (variable, narrow athletic) | `--font-display` | `font-display` | Headlines, numerals, masthead, stat figures |
| **Hanken Grotesk** (variable) | `--font-sans` | `font-sans` | Body, labels, captions, UI text |

**Never** use Inter, Roboto, Arial, system fonts, or Space Grotesk — per the [feedback memory](file:///c:/Users/skyli/.claude/projects/d--Projects-EarlyEagles/memory/feedback_frontend_design.md), these are the AI-slop defaults the brand is built to avoid.

### 3.2 Type scale (Tailwind v4 defaults — applied via classes)

| Use | Class | Approx size | Weight |
|---|---|---|---|
| Hero masthead numeral / wordmark | `text-7xl` / `text-8xl` | 72-96px | 800-900 |
| Page headline (display) | `text-3xl` md:`text-5xl` | 30-48px | 800 |
| Section title (display) | `text-2xl` md:`text-[2.6rem]` | 24-42px | 700-800 |
| Featured stat figure | `text-6xl` md:`text-7xl` | 60-72px | 800-900 |
| Body | `text-base` | 16px | 400-500 |
| Caption | `text-sm` | 14px | 400 |
| Editorial label | `text-[0.6875rem]` | 11px | 600-700, uppercase, tracking-[0.16em] |

### 3.3 Utility classes (defined in `globals.css`)

| Class | Purpose | Implementation |
|---|---|---|
| `.display` | Athletic display type | Big Shoulders, weight 800, uppercase, tight tracking, line-height 0.95 |
| `.figures` | Tabular + lining numerals | `font-feature-settings: "tnum", "lnum"` — use on numbers in charts, stats, tables |
| `.label` | Section labels | Hanken Grotesk, 11px, 700, 0.16em tracking, uppercase, muted-foreground |
| `.label-accent` | Section label with green underline | Extends `.label` with a 1.75rem-wide primary-color underline |

### 3.4 Typography rules

- **Do** use `.display` for headlines, masthead, big numerals — anything that should *announce*.
- **Do** use `.figures` on every numeric value in stat figures, chart axes, ledger tables. Tabular numerals are non-negotiable for data.
- **Do** use uppercase + wide tracking (`tracking-[0.16em]` to `tracking-[0.22em]`) for labels and meta text.
- **Don't** mix display fonts. One display, one body. Adding a third is forbidden.
- **Don't** use italic display type — Big Shoulders italic doesn't exist; using a fake italic looks broken.
- **Don't** put body text under 14px. The system has no `text-xs` for prose — only for labels.

## 4. Spacing

Tailwind v4 default spacing scale, used directly. The dashboard composes from these breakpoints:

| Token | Use |
|---|---|
| `gap-1` / `gap-2` | Inline elements (chip + count) |
| `gap-3` / `gap-4` | Stat-card internals |
| `gap-6` / `gap-8` | Section internals |
| `gap-12` md:`gap-16` | Between major page sections |
| `py-8` md:`py-12` | Page top/bottom padding |
| `px-5` md:`px-8` lg:`px-12` | Horizontal page padding |

**Rule:** Don't go denser than `gap-3` for visible content. Don't go sparser than `gap-16` between sections. Anything outside this range looks broken.

## 5. Border radius

`--radius` = `0.375rem` (6px). The athletic aesthetic favours **almost-square corners** — softer than zero, sharper than the shadcn default of `0.625rem`. Tailwind classes:

| Class | Computed | Use |
|---|---|---|
| `rounded-sm` | 0.225rem | Inline chips, reaction emoji backgrounds |
| `rounded` (md) | 0.3rem | Buttons, inputs |
| `rounded-md` | 0.3rem | Default for cards |
| `rounded-lg` | 0.375rem | Stat figures, primary cards |
| `rounded-xl` | 0.525rem | Hero cards, large surfaces |

**Don't** use `rounded-full` for anything except avatars, reaction chips, the live-pulse dot.

## 6. Motion

Motion library (`motion/react`), installed and configured. Three patterns, all defined in [`apps/web/components/editorial/reveal.tsx`](../../apps/web/components/editorial/reveal.tsx).

### 6.1 Patterns

| Pattern | What | Where |
|---|---|---|
| **Staggered reveal** | Page sections fade-up with blur, 90ms stagger between siblings | Wrap a section list in `<RevealStack>` with `<RevealItem>` children |
| **Live pulse** | 1.6s ease-in-out scale + opacity loop on a red dot | `<span className="live-dot" />` — pure CSS, no JS |
| **Chart draw-in** | Recharts default animation, ~750ms cubic-bezier | Already on by default; don't disable it |

### 6.2 Motion rules

- **Do** honor `prefers-reduced-motion` — the existing `useReducedMotion()` hook in `RevealStack` and `RevealItem` already handles this. Any new motion must check it too.
- **Do** keep entrance reveals to *one* per page load. Staggering the masthead → hero → cards is one orchestrated moment. Don't add a second wave on scroll.
- **Don't** add hover motion to everything. Hover should be subtle — color shift, slight underline, *not* bounce/scale.
- **Don't** use spring physics for utility elements (buttons, inputs). Reserve spring for the playful surfaces (event RSVPs, reactions).
- **Don't** animate `width` / `height` / `margin` — these trigger layout. Animate `transform` / `opacity` / `filter` only.

## 7. Atmospheric details

Two background atmosphere elements ship in `globals.css`. They are *additive layers*, not load-bearing:

### 7.1 Court-line grid (subtle)

A 64×64 grid of hairlines, faded through a radial mask, at 5% opacity. Inspired by a badminton court. Visible only as texture, never as structure.

### 7.2 Ambient radial glows

Green at top-center, red at bottom-right. Both at 12% / 8% opacity via `color-mix`. Create depth without shouting.

### 7.3 Atmosphere rules

- **Do** remove these layers if they ever read as gimmicky — they're additive and trivially removable.
- **Don't** add a third atmosphere layer. Two is the limit.
- **Don't** apply atmosphere to dialogs, modals, dropdowns — those need clean surfaces.

## 8. Built primitives

Reusable components already shipping in the codebase. Compose these before building new ones.

| Component | File | Use |
|---|---|---|
| `<Masthead>` | [editorial/masthead.tsx](../../apps/web/components/editorial/masthead.tsx) | Page header with live strip, wordmark, accent strike |
| `<SectionHeader>` | [editorial/section-header.tsx](../../apps/web/components/editorial/section-header.tsx) | Kicker + display title + meta |
| `<StatFigure>` | [editorial/stat-figure.tsx](../../apps/web/components/editorial/stat-figure.tsx) | Big-numeral card with optional delta + caption + primary emphasis |
| `<Rule>` | [editorial/rule.tsx](../../apps/web/components/editorial/rule.tsx) | Hairline / fine / bold separator; `tone="primary"` for green |
| `<RevealStack>` / `<RevealItem>` | [editorial/reveal.tsx](../../apps/web/components/editorial/reveal.tsx) | Staggered entrance reveal |
| `<ChartContainer>` etc. | [ui/chart.tsx](../../apps/web/components/ui/chart.tsx) | shadcn chart primitives (Recharts wrapped) |
| Sample charts | [components/charts/](../../apps/web/components/charts/) | Bar / line / area patterns with athletic palette |

## 9. Not-yet-built — design language placeholders

Components that need to be styled in the athletic register before they ship. These don't have a visual identity yet — when each one lands, follow the rules above.

| Component | Status | Notes |
|---|---|---|
| Forms (input, select, textarea, checkbox, radio, date-picker) | Not styled | Use shadcn primitives; override defaults with primary-green focus rings, tabular numerals on numeric inputs |
| Mobile bottom nav | Wireframed in [member-landing.md](member-landing.md), not built | P0-070.2 |
| Theme switcher (light / dark / system) | Not built | P0-070.3 — toggle adds/removes `.light` on `<html>` |
| Empty states | Not styled | Default to a short kicker + headline + secondary action. No giant illustrations. |
| Modals / drawers / toasts / command palette | Not styled | shadcn primitives exist; need athletic overrides |
| Loading skeletons | Not styled | Use `--rule` color + subtle pulse, never glitter shimmer |
| Error pages (404 / 500 / 403) | Not styled | Athletic display number + short copy + primary action |

When you build any of the above, **add it to this list** with a "Built ✓" entry and a file link.

## 10. The do-not list (compressed)

Everything that has been explicitly rejected, in one place. Skip this list at your peril.

- ❌ Generic AI-slop fonts: Inter, Roboto, Arial, system-ui, Space Grotesk
- ❌ Purple gradients on white
- ❌ Editorial cream / serif / claret aesthetic — prototyped + rejected 2026-05-24
- ❌ Light mode as the production default
- ❌ Hard-coded hex / rgb colors in TSX
- ❌ A third font family
- ❌ A third atmosphere layer
- ❌ `rounded-full` outside avatars, chips, and pulse dots
- ❌ Hover animations on every interactive element
- ❌ Layout-triggering motion (`width`, `height`, `margin`)
- ❌ Bangla / i18n strings — deferred per [ADR-0003](../adr/0003-defer-i18n.md) until a Bangla-first tenant approaches
- ❌ Free-form text posts / Discord-style feed — out of scope per [ADR-0005](../adr/0005-light-social-layer.md)
- ❌ Shuttle-yellow as a primary action color — it's accent only

## 11. References

- **Feedback memory:** [`feedback_frontend_design`](file:///c:/Users/skyli/.claude/projects/d--Projects-EarlyEagles/memory/feedback_frontend_design.md) — the locked aesthetic decision
- **ADR-0003:** [defer i18n](../adr/0003-defer-i18n.md) — English-only typography decisions
- **ADR-0004:** [role-based landing](../adr/0004-role-based-landing-and-routing.md) — URL → surface mapping
- **ADR-0005:** [light social layer](../adr/0005-light-social-layer.md) — what content types exist
- **Wireframe:** [member-landing.md](member-landing.md) — applied example of this system
- **Live tokens:** [apps/web/app/globals.css](../../apps/web/app/globals.css) — source of truth, if this doc drifts the code wins
- **Skill:** [`.claude/skills/frontend-design`](../../.claude/skills/frontend-design/SKILL.md) — invoked before every UI change

## 12. When this document drifts from the code

The code (`globals.css`, the editorial components, the layout fonts) is the ground truth. If a future change adds a token, removes one, or swaps a font, **update this doc in the same PR**. A drifted design system is worse than no design system — it lies to whoever reads it.

If you find a drift while implementing something, fix the drift first, then continue.

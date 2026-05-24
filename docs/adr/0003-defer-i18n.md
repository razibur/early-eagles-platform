# ADR-0003: Defer English + Bangla i18n until a real Bangla-speaking user is near

- **Status:** Accepted (supersedes the "i18n from day one" framing in [ADR-0002](./0002-enterprise-saas-reframe.md) and the matching conventions in `CLAUDE.md`)
- **Date:** 2026-05-24
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Plan.md`](../../Plan.md), [`project-scope.md`](../../project-scope.md), [`CLAUDE.md`](../../CLAUDE.md), [`Implementation-Plan.md`](../../Implementation-Plan.md)

## Context

ADR-0002 and the `CLAUDE.md` project framing both required Bangla strings alongside English from the first commit — "Languages: English + Bangla from day one at every layer" and "Do not 'add translations later' — the architecture (`next-intl` + `IStringLocalizer`) makes both equally easy."

That framing has not aged well in practice:

1. **No Bangla-speaking user is near production.** Tenant 0 (the Early Eagles club) communicates in English on WhatsApp; the project owner is comfortable in English; there is no second tenant with a Bangla-first audience scheduled.
2. **Solo-builder bandwidth is the binding constraint.** Wiring `next-intl` locale routing, message catalogs, ASP.NET `IStringLocalizer`, locale-aware emails, push, and Bangla font loading is a real chunk of work that produces zero value until a Bangla user reads a Bangla screen.
3. **Phase 0 already has more foundation tasks than the solo builder can realistically land before momentum dies** (backend Clean Arch, multi-tenancy, Clerk, audit, outbox, Bicep, CI/CD). Trimming optional foundation work is more valuable than insisting on it.
4. The "equally easy" claim in ADR-0002 is true *at the architecture level* but ignores the per-string ongoing cost: every UI string, every error message, every email needs a translation pass that nobody is qualified to write.

## Decision

**Defer i18n entirely.** Ship English-only through Phase 0, Phase 1, and Phase 2.

Re-introduce internationalization when **either** of the following becomes true:

- (A) A Bangla-first tenant is within ~one quarter of production go-live, **or**
- (B) The platform is about to be marketed to non-English clubs (public landing pages, sales outreach).

Whichever trigger fires first kicks off the i18n retrofit as its own focused effort.

## Consequences

### Saved now
- Skip `P0-066` (next-intl + locale routing), `P0-069` (Bangla font loading), and the i18n parts of `P0-054` (email template rendering).
- Skip parallel English / Bangla strings in every new feature for the foreseeable future.
- Skip `IStringLocalizer` wiring on the backend; backend error messages and emails are English-only.

### Cost paid later
- A retrofit pass will be needed: extract hard-coded strings into message catalogs, translate everything that exists, install `next-intl`, install `IStringLocalizer`, add locale switcher, load Bangla font.
- Cost grows with each English-only page / email / error. **Mitigation:** keep user-visible strings out of business logic (which is good practice regardless under Clean Architecture). Treat tsx files as the only place strings live on the frontend, and resource files / message constants as the only place on the backend. This keeps the eventual extraction mechanical.

### Documentation impact
- `CLAUDE.md` Project framing line "Languages: English + Bangla from day one at every layer" is **removed**.
- `CLAUDE.md` Convention "i18n at every layer" is **replaced** with a "i18n is deferred per ADR-0003" note.
- `Implementation-Plan.md` tasks `P0-066`, `P0-069`, and i18n acceptance lines should be marked **deferred** with a pointer to this ADR rather than deleted (they will come back when the trigger fires).
- ADR-0002 itself stays as-is per the immutable-ADR convention; its i18n framing is now historical, superseded by this ADR.

### Risk acknowledged
If the platform unexpectedly attracts a Bangla-first tenant before the retrofit happens, that tenant ships in English on day one and we apologize. The owner accepts this risk explicitly given the value of speed and the absence of any concrete Bangla-first tenant today.

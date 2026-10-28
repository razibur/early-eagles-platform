<!--
Title: "<task ID>: <imperative summary>", e.g. "P0-048.2: restrict /audit-events to tenant admins".
Conventions: CONTRIBUTING.md.
-->

## What

<!-- What changed and why, in a few sentences. Link the task row in PROGRESS.md. -->

Task: P0-

## How it was verified

<!-- What you ran or checked, with results. Name anything you could NOT check (e.g. "live Azure deploy carried to P0-059.3"). -->

-

## Checklist

- [ ] The task's `verify:` step passes, and its PROGRESS.md row is ticked with what was actually checked
- [ ] `docs/developer-walkthrough.md` covers the change (plus version pins / gotchas if affected)
- [ ] Backend: `dotnet build` (warnings are errors), unit tests, and the **tenant-isolation gate** (integration tests) pass
- [ ] Frontend: `npm run lint`, `npm run build` and `npm run test:e2e` pass
- [ ] Bicep: `az bicep build` + lint clean for every touched `main.<env>.bicep`
- [ ] New domain entities have a `TenantId` and are covered by the global query filter
- [ ] State changes go through Mediator so `AuditBehavior` records them
- [ ] New user-facing capability is behind a feature flag, off by default in prod
- [ ] New cookies or storage keys are in `docs/cookie-policy.md` and on `/cookies`
- [ ] No secrets in the diff
- [ ] Stack or architecture change? A new ADR, not an edit to an accepted one

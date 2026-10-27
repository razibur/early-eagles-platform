# Contributing

This is a proprietary repository (see [`LICENSE`](./LICENSE)). Contributions are by invitation.
Everyone taking part follows the [Code of Conduct](./CODE_OF_CONDUCT.md).

Before writing code, read [`PROGRESS.md`](./PROGRESS.md) (what's done and what's next) and
[`CLAUDE.md`](./CLAUDE.md) (the non-negotiable conventions). The
[developer walkthrough](./docs/developer-walkthrough.md) explains how everything was built.

## Work comes from task IDs

Every change implements a task from [`Implementation-Plan.md`](./Implementation-Plan.md),
referenced by its stable ID (`P0-034`, `P1-014`, …). Tasks are small: **1–3 hours, one
concept, independently verifiable, committable alone.** If a task turns out bigger, split it
into dotted sub-IDs (`P0-062.2.1`, `P0-062.2.2`, …) and **append** the split to
`Implementation-Plan.md` instead of rewriting the original row.

No task fits? Add one to the plan first, then do it.

## Branches

`main` is always releasable. Work on a short-lived branch named after the task:

```text
p0-048/audit-query-endpoint
p1-014.2/attendance-checkin-api
fix/p0-067.2-maskable-icon-padding
```

Lowercase, task ID first, then a few words. Rebase on `main` before opening a PR. Don't merge
`main` into your branch.

## Commits

```text
P0-034.3: tenant isolation test for cross-tenant query

Optional body: why, not what. Wrap at ~72 characters. Mention
anything a reviewer can't see from the diff (a live check that was
skipped, a follow-up task that was created).
```

- **Subject:** `<task ID>: <imperative summary>`, at most ~72 characters, no trailing period.
  Several tasks in one commit: `P0-062.4, P0-063: …`. A range of sub-tasks:
  `P0-062.3.1–P0-062.3.3: …`.
- Changes that aren't a task use the document as the prefix: `ADR-0011: …`, `docs: …`.
- One task per commit where possible, so each can be reverted alone.
- AI-assisted commits keep the `Co-Authored-By:` trailer the tool adds.

## Pull requests

**Title:** same format as the commit subject. **Description:** what changed, how you verified
it, and anything skipped. Before asking for review:

- [ ] The task's `verify:` step passes, and its row in `PROGRESS.md` is ticked with the verify
      line updated to say what you actually checked.
- [ ] `docs/developer-walkthrough.md` covers the change (new section or update, plus the version
      pin and gotcha appendices if they're affected).
- [ ] Backend: `dotnet build` (warnings are errors) and both test projects pass, including the
      **tenant-isolation gate** (`EarlyEagles.Tests.Integration`).
- [ ] Frontend: `npm run lint` and `npm run build` pass.
- [ ] Bicep: `az bicep build` and lint are clean for every `main.<env>.bicep` you touched.
- [ ] New domain entities have a `TenantId` and are covered by the global query filter.
- [ ] State changes go through Mediator so `AuditBehavior` records them.
- [ ] New user-facing capabilities sit behind a feature flag, off by default in prod.
- [ ] No secrets in the diff. Config goes through Key Vault or environment variables.

## Decisions

A change of stack, architecture or cross-cutting convention needs an
[ADR](./docs/adr/): copy [`0000-template.md`](./docs/adr/0000-template.md) to the next number.
**Accepted ADRs are never edited.** A new ADR supersedes the old one.

## Out of scope

Don't add what [`CLAUDE.md`](./CLAUDE.md#out-of-scope-do-not-add-unless-asked) lists as out of
scope (online dues collection, a generic tournament engine, and others) unless a task asks for it.

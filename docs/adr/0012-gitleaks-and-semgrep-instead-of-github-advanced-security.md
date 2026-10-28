# ADR-0012: gitleaks and Semgrep CE in CI instead of GitHub secret scanning and CodeQL

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) P0-081, P0-082;
  [`docs/runbooks/security-scanning.md`](../runbooks/security-scanning.md)

## Context

P0-081 asked for GitHub secret scanning with push protection, and P0-082 for a CodeQL workflow for C#
and TypeScript. The repository (`razibur/early-eagles-platform`) is **private**. On a private repo
both features are paid add-ons (GitHub Secret Protection and GitHub Code Security, priced per
committer); they are free only on public repos. The cost posture is
"as free as possible", and the code isn't going public while it's being built to sell.

The need is still real. The platform will hold several clubs' data, enterprise buyers ask about
secret handling and static analysis in security reviews, and a leaked Azure or Stripe credential is
the cheapest way to lose a tenant's trust.

## Decision

A new workflow, [`.github/workflows/security.yml`](../../.github/workflows/security.yml), runs on
every PR to `main`, every push to `main`, and weekly. It has two jobs, and both fail on any finding:

- **`secrets (gitleaks)`:** gitleaks **8.30.1** scans the full git history (`fetch-depth: 0`) with the
  built-in rules. [`.gitleaks.toml`](../../.gitleaks.toml) extends them with one narrow allowlist
  for the public Azure built-in role IDs in the Bicep files.
- **`sast (semgrep)`:** Semgrep CE **1.180.0** runs the community `p/default` ruleset, which covers
  C#, TypeScript, Dockerfiles and the workflow files. It sends no metrics, and neither job needs an
  account or a token.

Both tools run as container images pinned by tag and digest. Findings are read in the job log; there
is no SARIF upload, because GitHub's code-scanning UI is part of the paid product.

The first scan found two real issues, and both were fixed in the same change. Every action in
every workflow is now pinned to a full commit SHA (with the version in a comment, which Dependabot
keeps updated), and the API Dockerfile states `USER $APP_UID`.

## Consequences

**Positive**

- $0. Both tools are open source and run on the free GitHub Actions minutes of a private repo.
- PRs get the same coverage the paid features give: secrets are caught in history, and known-bad
  code patterns are caught in C#, TypeScript, Docker and the workflows.
- Both run identically on a laptop with Docker (see the runbook), so a finding can be reproduced
  and fixed before pushing.

**Negative / costs**

- **No push protection.** GitHub's version blocks a secret before it reaches the server. Here a
  pushed secret is caught *after* the push, by a failing check. A caught secret must be **rotated**,
  not just deleted, because it's in history and in GitHub's copy. A local pre-commit hook
  (`gitleaks git --staged` or `gitleaks protect`) narrows the gap but is opt-in per machine.
- **No partner revocation.** GitHub notifies providers (Azure, Stripe and others) so they revoke
  leaked tokens automatically. That doesn't happen here.
- **Shallower analysis than CodeQL.** Semgrep CE's community rules match patterns within one file;
  CodeQL follows data across files (taint tracking). Cross-file injection paths are less likely to be
  caught.
- **Two image pins to keep current by hand.** Dependabot updates the `uses:` SHAs but not the
  `docker run` images inside `run:` steps.

**Follow-ups**

- P0-081 and P0-082 are reworded to these tools; see the Implementation-Plan appendix of
  2026-10-28.
- Make `secrets (gitleaks)` and `sast (semgrep)` required checks in branch protection (P0-015).

## Alternatives considered

| Option | Why not |
|---|---|
| GitHub Secret Protection + Code Security on the private repo | paid per committer; against the cost posture while there's no revenue |
| Make the repo public (both features free) | it's a product being built to sell; the source isn't meant to be open |
| CodeQL CLI in Actions without GHAS | the CodeQL licence doesn't allow analysing private, non-open-source code without a GHAS licence |
| `gitleaks/gitleaks-action` | adds a JavaScript action wrapper around the same binary; running the pinned image is simpler and has one fewer dependency |
| Semgrep AppSec Platform (cloud, free tier) | needs an account, a token and code uploaded to a third party for cross-file analysis; CE in CI is enough for now |
| TruffleHog | also good; gitleaks was picked for its simpler config and the rule-allowlist model. Either would do. |

## Revisit triggers

- The repo goes public, or the project buys GitHub Team/Enterprise with GHAS. Then turn on native
  secret scanning with push protection plus CodeQL, and retire this workflow (in a new ADR).
- A secret reaches `main` despite this setup. Then add the pre-commit hook to the README quick start
  as a required step.
- An enterprise customer's security review requires cross-file SAST.

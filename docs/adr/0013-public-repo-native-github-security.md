# ADR-0013: Public repository, with GitHub's native security features on top of gitleaks and Semgrep

- **Status:** Accepted (supersedes the "Revisit triggers → retire this workflow" part of [ADR-0012](./0012-gitleaks-and-semgrep-instead-of-github-advanced-security.md))
- **Date:** 2026-10-28
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Implementation-Plan.md`](../../Implementation-Plan.md) P0-015, P0-081, P0-082;
  [`docs/runbooks/security-scanning.md`](../runbooks/security-scanning.md)

## Context

On 2026-10-28 GitHub refused branch protection on the private repo (`403 Upgrade to GitHub Pro or make
this repository public`). The owner chose to make `razibur/early-eagles-platform` **public** rather than
pay. The code stays proprietary: [`LICENSE`](../../LICENSE) reserves all rights, so being able to read
the code does not make it legal to reuse.

On a public repo these are free: branch protection, secret scanning with push protection, CodeQL, and
Dependabot alerts and security updates. ADR-0012's revisit trigger said to switch to the native
features and retire the gitleaks/Semgrep workflow.

## Decision

1. **Branch protection on `main`:**
   - every change arrives through a PR, with 0 approvals required (there's one maintainer);
   - six required checks, and the branch must be up to date with `main`: `build + unit tests`,
     `integration tests (tenant-isolation gate)`, `lint + build`, `browser tests (Playwright)`,
     `secrets (gitleaks)`, `sast (semgrep)`;
   - unresolved review conversations block the merge;
   - no force pushes, no deletion;
   - **enforced for admins too**.

   Signed commits are not required yet: the existing history and the local setup aren't signed.
2. **Native secret scanning and push protection: on.** GitHub now blocks a known-provider secret
   before the push lands and alerts on history, which closes the gap ADR-0012 accepted. Generic
   patterns and validity checks stay off, because they need the paid product even on a public repo.
3. **Dependabot alerts and security updates: on.** Security PRs arrive outside the weekly schedule and
   the 14-day cooldown.
4. **CodeQL** ([`.github/workflows/codeql.yml`](../../.github/workflows/codeql.yml)): C# (build mode
   `none`), JavaScript/TypeScript and Actions, with the `security-extended` query suite, on PRs,
   pushes and weekly. Results appear in the Security tab and as PR annotations. It's **not a
   required check** yet.
5. **Keep gitleaks and Semgrep** (`security.yml`) instead of retiring them. They cost nothing and are
   already required checks. They catch things the native features don't: generic-pattern secrets
   (gitleaks) and Dockerfile and workflow rules (Semgrep). They also run on a laptop with Docker.

## Consequences

**Positive**

- `main` can only change through a green PR, which enforces the tenant-isolation gate for the first
  time instead of relying on habit.
- Secrets are blocked before they're pushed, and GitHub tells providers about leaked partner tokens.
- Cross-file taint analysis (CodeQL) on top of pattern matching (Semgrep).

**Negative / costs**

- **Anyone can read the code**, including the architecture, the runbooks and the commit history (the
  committer email is visible). Licence aside, a competitor can learn from it. Secrets are kept out by
  design (Key Vault, `.env`, managed identities) and checked three ways.
- Every change, even a one-line doc fix, goes through a PR and waits for the checks (about 2–3
  minutes).
- Some overlap between the two secret scanners and the two SAST tools means more CI minutes, which are
  free on public repos.

**Follow-ups**

- Decide whether CodeQL becomes a required check once its first results are triaged.
- Consider signed commits (SSH signing) and then `required_signatures`.
- The `LICENSE` text says "confidential"; the owner may want to reword it for a public repo.

## Alternatives considered

| Option | Why not |
|---|---|
| GitHub Pro (paid) and keep the repo private | the owner chose public over paying |
| Public, and retire gitleaks + Semgrep as ADR-0012 suggested | native secret scanning only knows provider patterns, and CodeQL doesn't scan Dockerfiles; the overlap costs nothing |
| Branch protection without `enforce_admins` | the only committer is an admin, so it would protect nothing |
| CodeQL "default setup" (configured in the GitHub settings UI) | not in the repo, so changes aren't reviewed or versioned; the workflow file keeps the configuration in git like the rest of CI |

## Revisit triggers

- The repo goes private again. Branch protection, push protection and CodeQL would then stop
  working, and ADR-0012's setup becomes the only scanning.
- A second maintainer joins. Then require 1 approving review and code-owner review.
- CodeQL produces noise. Tune the query suite, or keep it advisory.

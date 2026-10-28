# Runbook — security scanning: secrets and SAST (P0-081, P0-082)

What the `security` workflow checks, how to run the same scans on your machine, and what to do when
one fails. Why these tools and not GitHub's paid ones: [ADR-0012](../adr/0012-gitleaks-and-semgrep-instead-of-github-advanced-security.md).

| Job | Tool | Scans | Fails on |
|---|---|---|---|
| `secrets (gitleaks)` | gitleaks 8.30.1 | the full git history | any match of the built-in rules not covered by [`.gitleaks.toml`](../../.gitleaks.toml) |
| `sast (semgrep)` | Semgrep CE 1.180.0, `p/default` | tracked files: C#, TypeScript, Dockerfiles, workflows, … | any finding |

It runs on PRs to `main`, pushes to `main`, and Mondays at 06:17 UTC, so new rules also catch old code.
Both jobs are required checks on `main`.

Since the repo went public ([ADR-0013](../adr/0013-public-repo-native-github-security.md)), GitHub's own
features run as well:

| Feature | What it adds | Where to look |
|---|---|---|
| Secret scanning + **push protection** | blocks a push containing a known provider's token *before* it lands; alerts on history | the push error itself; Security → Secret scanning |
| CodeQL ([`codeql.yml`](../../.github/workflows/codeql.yml)) | cross-file taint analysis for C#, JS/TS and workflows (`security-extended`); advisory, not required | PR annotations; Security → Code scanning |
| Dependabot alerts + security updates | vulnerable dependencies, with fix PRs outside the weekly schedule | Security → Dependabot |

If push protection blocks a push, the secret never reached GitHub: remove it from the commit (amend
or rebase) and rotate it only if it might have leaked elsewhere. Don't use the "allow secret" bypass
link unless it really is a test value.

## 1. Run it locally

From the repo root in Git Bash, with Docker running. The `MSYS_NO_PATHCONV=1` prefix stops Git Bash
from rewriting `/repo` into a Windows path.

```bash
# Secrets, whole history (what CI runs)
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 \
  git /repo --config /repo/.gitleaks.toml --redact --no-banner -v

# Secrets, only what you're about to commit
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 \
  git /repo --staged --config /repo/.gitleaks.toml --redact --no-banner -v

# SAST
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/src" -w /src semgrep/semgrep:1.180.0 \
  semgrep scan --config p/default --metrics=off --error
```

Don't use gitleaks' `dir` mode on the working tree: it walks `node_modules`, `.next` and `bin`,
which aren't in git, and reports build output (Next's prerender manifests hold random preview keys).

## 2. The secrets job failed

1. **Open the log.** It names the file, line, commit and rule; the value itself is redacted.
2. **Is it a real secret?** If there's any doubt, treat it as real.
3. **Real: rotate it first.** Revoke and reissue the credential at its provider (Azure, Clerk,
   Stripe, …) *before* touching the code. Deleting the line doesn't help: the value is in git history
   and in GitHub's copy of the branch. Then move the new value to Key Vault or `.env` (both are
   outside git) and remove it from the code. Rewriting history is optional once the key is dead.
   If it reached `main`, follow [`incident-response.md`](./incident-response.md) and record it.
4. **Not a secret** (a public ID or a test fixture): add the narrowest possible entry to
   `.gitleaks.toml` with a `description` saying why it's public, then re-run the planted-secret check
   in §4.
   A one-off can instead carry an inline `gitleaks:allow` comment on the line.

## 3. The SAST job failed

Read the rule link in the log (`https://sg.run/…`). Fix the code; that's the normal outcome.
If it's a false positive, suppress it on the line with a `nosemgrep: <rule-id>` comment that says why.
Never turn a rule off for the whole repo without an ADR.

Findings fixed at adoption, 2026-10-28:

| Rule | Fix |
|---|---|
| `github-actions-mutable-action-tag` (18 places) | every `uses:` pinned to a full commit SHA with `# vX.Y.Z` after it; Dependabot updates both |
| `missing-user-entrypoint` (`apps/api/Dockerfile`) | `USER $APP_UID` stated explicitly (the chiseled image already ran as uid 1654) |

## 4. Changing `.gitleaks.toml` — the planted-secret check

An allowlist that's too broad silences real leaks without any error. Two traps found while writing
the current one:

- **Conditions are OR'd by default.** An allowlist with both `paths` and `regexes` skips a finding when
  *either* matches. A `paths` entry for `infra/*.bicep` therefore silenced every Bicep finding,
  including a planted Stripe key. Either set `condition = "AND"` or don't combine them.
- **`^…$` anchors didn't match** with `regexTarget = "line"`. Leave them off and make the pattern
  specific instead.

After any change, plant fakes in a scratch folder and check that the real one is still caught:

```bash
mkdir -p /tmp/leak/infra
key=$(python -c "import secrets,string;print(''.join(secrets.choice(string.ascii_letters+string.digits) for _ in range(40)))")
printf "var stripeKey = 'sk_live_%s'\nvar keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'\n" "$key" > /tmp/leak/infra/x.bicep  # gitleaks:allow (public role ID sample)
MSYS_NO_PATHCONV=1 docker run --rm -v "$(cygpath -w /tmp/leak):/repo" -v "$(pwd -W)/.gitleaks.toml:/cfg.toml" \
  ghcr.io/gitleaks/gitleaks:v8.30.1 dir /repo --config /cfg.toml --redact --no-banner -v
```

Expect exactly one finding: `stripe-access-token` on line 1. The role ID on line 2 must stay
allowed. Use a random value, because gitleaks ignores keys that look like placeholders
(`…abcd0123456789`).

## 5. Keeping the tools current

Dependabot updates the `uses:` SHAs, but not the two images in `security.yml`'s `run:` steps. When you
bump one, pick a release at least two weeks old, pin the tag and its digest
(`docker image inspect <image>:<tag> --format '{{index .RepoDigests 0}}'`), run §1 locally with the
new version, and update this page and Appendix A of the developer walkthrough. Leave ADR-0012 alone:
it records the choice of tools, not the current versions.

## 6. Optional: a pre-commit hook

To catch a secret *before* it leaves your machine (CI only catches it after the push):

```bash
cat > .git/hooks/pre-commit <<'EOF'
#!/bin/sh
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W 2>/dev/null || pwd):/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 \
  git /repo --staged --config /repo/.gitleaks.toml --redact --no-banner
EOF
chmod +x .git/hooks/pre-commit
```

It adds a few seconds to each commit and needs Docker running. `git commit --no-verify` skips it, so
CI stays the real gate.

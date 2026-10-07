# ADR-0010: GitHub Container Registry instead of Azure Container Registry

- **Status:** Accepted (supersedes the container-registry design added in the Implementation-Plan appendix "Container registry added (2026-09-28)" and built in P0-058.6 / P0-059.4)
- **Date:** 2026-10-06
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [ADR-0009](./0009-local-dev-no-cloud-dev-environment.md), [`infra-conventions.md`](../infra-conventions.md) §1 / §3.2 / §4 / §7, [`PROGRESS.md`](../../PROGRESS.md)

## Context

P0-058.6 added one Basic Azure Container Registry shared by every environment
(`creesharedeus<suffix>` in `rg-ee-shared-eus`, deployed by `main.shared.bicep`). Under the
"as free as possible" posture and [ADR-0009](./0009-local-dev-no-cloud-dev-environment.md),
the standing Azure bill is prod (~$16) plus that registry (~$5), so the registry is about a
quarter of it.

GitHub Container Registry (`ghcr.io`) hosts private images, and GitHub's billing docs state that
container image storage and bandwidth are **currently free**, with at least one month's notice
before that changes. CI already runs on GitHub Actions, which can push to GHCR with its built-in
`GITHUB_TOKEN`, so no Azure credential is needed to publish an image.

Nothing had been deployed, so removing the registry costs nothing.

## Decision

- Images are published to **`ghcr.io/razibur/early-eagles-api`** (private, linked to the
  repository). CI (P0-084) pushes with `GITHUB_TOKEN` and `packages: write`.
- **Removed:** `infra/modules/containerRegistry.bicep`, `infra/main.shared.bicep` and its
  parameter file, the `rg-ee-shared-eus` resource group, and the `shared` environment code.
- **Pulling:** the image is private, and Container Apps can't use a managed identity against
  GHCR. The app pulls with a GitHub token scoped to **`read:packages` only**, stored in Key Vault
  and referenced from the Container App's registry settings through its managed identity.
  P0-062.4 is redefined to do this instead of granting `AcrPull`.

## Consequences

- Standing Azure bill: **~$16/month** (prod only) while staging is not deployed.
- A long-lived secret now exists: the `read:packages` token. It can only read packages, lives
  only in Key Vault, and needs rotating before it expires. Rotation is a runbook step (P0-063).
- The pull token belongs to a personal GitHub account. If the project moves to a GitHub
  organization, the token moves to a machine account.
- If GitHub starts billing GHCR, the fallback is to restore the registry module from git history
  (`208637a`, `8cf194d`) and revert P0-062.4 to `AcrPull`. Both are recoverable in an afternoon.
- Without Azure Container Registry, Microsoft Defender image scanning on push isn't available.
  GitHub-side scanning (e.g. Trivy in CI) covers it when the CI tasks land.

# ADR-0009: Dev runs locally; no standing cloud dev environment

- **Status:** Accepted (supersedes the "dev → staging → prod" environment line of [ADR-0002](./0002-enterprise-saas-reframe.md)'s CI/CD pick)
- **Date:** 2026-10-06
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`infra-conventions.md`](../infra-conventions.md) §1 / §7, [`Implementation-Plan.md`](../../Implementation-Plan.md) appendix "Local dev instead of cloud dev", [`PROGRESS.md`](../../PROGRESS.md)

## Context

ADR-0002 set up three Azure environments — dev (deployed on every merge to `main`), staging and
prod. The owner's cost direction is "as free as possible" (infra-conventions §7, cost posture).
With the infra code at minimum sizes, a standing cloud dev environment is still the larger half
of the Azure bill:

| | Dev | Prod |
|---|---|---|
| Postgres `B1ms` + 32 GB | ~$16 | ~$16 |
| Service Bus Standard | ~$10 | — |
| **Per month** | **~$26** | **~$16** |

Everything dev provides can run on the developer's machine: Postgres already runs as a
`postgres:17` container (Testcontainers in the integration suite), and Microsoft ships an Azure
Service Bus emulator as a container image.

## Decision

- **Dev is local.** A `docker compose` stack (P0-059.5) runs Postgres 17 and the Service Bus
  emulator; the API and web app run against it with `dotnet run` / `npm run dev`. No `rg-ee-dev-eus`
  is deployed.
- **Prod is the only standing Azure environment**, plus the shared container registry.
- **Staging is on demand**: `main.staging.bicep` stays, and is deployed when a release needs a
  production-shaped rehearsal, then torn down.
- **`main.dev.bicep` stays as the smoke-test template.** It is deployed only with
  `EE_SANDBOX=true` (now the parameter file's default) into `rg-ee-sandbox-eus`, to prove the
  modules deploy (P0-059.3), and deleted the same day.
- **CI:** a merge to `main` builds, tests and pushes the image to the registry; it no longer
  deploys anywhere. Prod deploys stay behind manual approval.

## Consequences

- Azure bill drops from ~$47 to **~$21/month** (prod ~$16 + registry ~$5) while staging is not
  deployed.
- "Works on my machine" risk moves to staging/prod: Entra auth to Postgres, Key Vault references
  and managed identity are only exercised in the cloud. Mitigation: the sandbox smoke test before
  the first prod deploy, and an on-demand staging deploy before risky releases.
- The Service Bus emulator needs a SQL container next to it and ~2 GB RAM; local topics are
  declared in the emulator's config file rather than in Bicep.
- Plan changes are recorded in the Implementation-Plan appendix rather than by rewriting tasks:
  `P0-059.3` deploys the sandbox only, `P0-084` becomes "build + push on merge", `P0-051`'s topics
  are created in the emulator config first, and in prod's `serviceBusTopics` when prod gets
  Service Bus.
- Reversible: a standing cloud dev environment is one `az deployment sub create` with
  `EE_SANDBOX=false`.

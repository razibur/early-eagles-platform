# ADR-0008: Azure Managed Redis instead of Azure Cache for Redis

- **Status:** Accepted (supersedes the "Cache + locks = Azure Cache for Redis" pick in [ADR-0002](./0002-enterprise-saas-reframe.md))
- **Date:** 2026-10-06
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`infra-conventions.md`](../infra-conventions.md) §4 / §7, [`infra/modules/redis.bicep`](../../infra/modules/redis.bicep), [`PROGRESS.md`](../../PROGRESS.md) P0-058.5

## Context

ADR-0002 picked **Azure Cache for Redis** for cache and distributed locks, and
[`infra-conventions.md`](../infra-conventions.md) §7 sized it as Basic `C0` in dev/stg and
Standard `C1` in prod.

Microsoft has since retired that product line. For Azure Cache for Redis Basic / Standard / Premium:

- New customers could not create instances from **2026-04-01**.
- Existing customers cannot create instances from **2026-10-01** — five days before this ADR.
- Existing instances retire on **2028-09-30**.

The Enterprise / Enterprise Flash tiers are already closed to new instances and retire on
2027-03-31. The named replacement is **Azure Managed Redis** (`Microsoft.Cache/redisEnterprise`),
built on Redis Enterprise.

This project has no Azure subscription yet (P0-003) and nothing is provisioned, so an
`Microsoft.Cache/redis` module written for P0-058.5 could never be deployed. There is no
migration cost — only the module and the docs change.

## Decision

Use **Azure Managed Redis** for cache and distributed locks. Everything ADR-0002 wanted from
Redis (hot-data caching, distributed locks, later sessions/backplane) is unchanged; only the
Azure product underneath it changes.

| | dev / stg | prod |
|---|---|---|
| SKU | `Balanced_B0` | `Balanced_B1` (confirm in P0-061) |
| High availability | off | on |

Fixed in the module, in every environment: Entra-only authentication (access keys disabled),
TLS 1.2+, no persistence, `NoCluster` clustering by default (the only policy that can be
changed in place later). Naming uses the CAF abbreviation `amr` with the §3.1 global-uniqueness
suffix: `amr-ee-<env>-<region>-<unique>`.

Alternatives considered:

- **Drop Redis for now; use Postgres advisory locks and in-process caching.** Viable at
  Tenant 0's scale and cheaper, but nothing in Phase 0 needs Redis before deploy, the module
  is only a parameter-file entry to omit, and ADR-0002 already argued Redis is needed at
  multi-tenant scale. Revisit if the dev bill becomes a problem — the `main.<env>.bicep`
  files can leave the module out without changing it.
- **Self-host Redis as a Container App.** No SLA, no managed patching, and a lock store that
  restarts on every revision — not appropriate for an enterprise product.

## Consequences

- Dev cost: roughly $13/month for `Balanced_B0` without HA, versus ~$16 for the old Basic `C0`.
  Verify in the pricing calculator for `eastus` before P0-059.3.
- Client changes: the endpoint is `<name>.<region>.redis.azure.net` on port **10000**
  (not 6380), and the API authenticates with Entra ID (`Microsoft.Azure.StackExchangeRedis`)
  — there is no access key. P0-062.3 grants the managed identity an access-policy assignment
  on the database (`Microsoft.Cache/redisEnterprise/databases/accessPolicyAssignments`), not
  an Azure RBAC role.
- No configurable maintenance window at the GA API version (`2025-07-01`); it exists only in
  previews. Accepted — revisit when it reaches GA.
- CLAUDE.md, Plan.md and `infra-conventions.md` are updated to say Azure Managed Redis.
  ADR-0002 is left as written, per the ADR immutability rule.

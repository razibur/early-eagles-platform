# Infrastructure Conventions

> **Read before writing a single line of Bicep.** Names and tags are the hardest
> thing to change after the fact: an Azure resource cannot be renamed, so fixing
> a bad name means recreating the resource — and for a database or a Key Vault
> that means downtime, a migration, or a soft-delete purge window you have to
> wait out. Deciding the rules once, before `infra/` exists, costs an hour.
> Deciding them after costs a weekend.

Task ID: **P0-056** in [PROGRESS.md](../PROGRESS.md) and
[Implementation-Plan.md](../Implementation-Plan.md). Everything under
**P0-057.\*** / **P0-058.\*** / **P0-059.\*** must follow this document.

---

## 0. Golden rules

1. **No portal clicks.** If it isn't in `infra/`, it doesn't exist. A resource
   created by hand is invisible to the next deploy and will be silently
   reverted, duplicated, or orphaned.
2. **No per-tenant resources.** This is a pool-model multi-tenant platform — one
   database, one namespace, one cache, `tenant_id` on every row. A resource name
   must never contain a tenant slug.
3. **Modules never hard-code names, regions or SKUs.** They take parameters;
   `main.<env>.bicep` supplies the values. A module that knows it is "dev" is a
   module that cannot build staging.
4. **Every resource carries the required tags** from §5. No exceptions, because
   the cost alert and the teardown script both key off them.
5. **No secrets in parameter files.** `*.bicepparam` is committed to git. Secrets
   come from Key Vault references or GitHub Actions secrets.

---

## 1. Subscription and resource-group layout

One Azure subscription, one resource group per environment.

| Environment | Code | Resource group | Purpose |
|---|---|---|---|
| Development | `dev` | `rg-ee-dev-eus` | Shared dev environment. Deployed on every merge to `main`. |
| Staging | `stg` | `rg-ee-stg-eus` | Production-shaped. Deployed on a `v*-staging` tag. |
| Production | `prod` | `rg-ee-prod-eus` | Deployed only on manual approval. |
| Shared | `shared` | `rg-ee-shared-eus` | Subscription-wide resources every environment uses — today only the container registry. Deployed by `main.shared.bicep` (P0-059.4) before any environment. |

A single subscription keeps one bill, one set of quotas and one place to look —
which is right for a solo operator on a $50–80/month envelope. **Split
production into its own subscription when** an enterprise customer requires
proof of blast-radius isolation, or when a runaway dev deploy could plausibly
exhaust a prod quota. That is a Phase 7 concern, not a Phase 0 one; revisit it
there rather than pre-building it now.

Throwaway experiments (P0-059.3) go in `rg-ee-sandbox-eus` and are deleted when
done. Deploy them with the environment file's `sandbox` switch (`main.dev.bicep`,
P0-059.1), which also changes the §3.1 suffix seed: a purge-protected Key Vault
keeps its name reserved after deletion, so a sandbox must never use the real
environment's globally unique names. Never experiment inside `rg-ee-dev-eus` — dev is a deploy target, not a
scratchpad.

---

## 2. Region

**Primary region: `eastus` (Virginia), abbreviated `eus`.**

Tenant 0 and the near-term customer base are US East Coast, so `eastus` puts the
database next to the users where it matters — every request pays the round trip,
and Postgres latency is the one thing a CDN cannot hide. It is also one of the regions where every
service in the stack (Container Apps, PostgreSQL Flexible Server, Service Bus,
Redis, SignalR) is available and generally cheapest — smaller US regions are
missing services or price above it, and finding that out mid-deploy is a bad
afternoon.

| Region | Code | Status |
|---|---|---|
| `eastus` | `eus` | Primary. Everything lives here. |
| `centralus` | `cus` | Reserved for a future paired-region failover. Not provisioned. |
| `canadacentral` | `cnc` | Reserved. Likely first data-residency ask (Canadian clubs). Not provisioned. |

**This is an assumption worth confirming before P0-059.3 deploys anything.** If
Tenant 0 is West Coast, or a Canadian club signs first and asks about residency,
change it *here* and in the parameter files — not by hand in the portal. Nothing
before P0-059.3 is expensive to redo.

Multi-region is explicitly out of scope for Phase 0. The region code is in every
name anyway, so adding one later does not force a rename.

---

## 3. Naming pattern

```
<type>-<workload>-<env>-<region>[-<instance>]
```

- **`<type>`** — the abbreviation from §4. Always first, so an alphabetical
  resource list groups by kind.
- **`<workload>`** — always `ee` (Early Eagles, the platform). Not the tenant.
- **`<env>`** — `dev` | `stg` | `prod` from §1.
- **`<region>`** — the code from §2.
- **`<instance>`** — `001`, `002`, … Include it only where more than one of the
  resource is plausible (container apps, databases). Omit it for singletons like
  the resource group or the Log Analytics workspace; a `-001` that will never
  have a `-002` is noise.

Examples:

```
rg-ee-dev-eus              resource group
log-ee-dev-eus             log analytics workspace
appi-ee-dev-eus            application insights
psql-ee-dev-eus-001        postgres flexible server
cae-ee-dev-eus             container apps environment
ca-ee-api-dev-eus-001      container app (api)
```

Note `ca-ee-api-…`: where one environment runs several instances of the same
resource type serving different roles, the role goes after the workload. Keep
role names short and stable (`api`, `worker`, `migrations`).

### 3.1 Globally unique names

Key Vault, Service Bus namespaces, Azure Managed Redis caches, storage accounts and
container registries share a global namespace, so the pattern alone is not enough — someone else may
already hold `kv-ee-dev-eus`. Append a deterministic suffix:

```bicep
var unique = take(uniqueString(subscription().subscriptionId, environment), 5)
var keyVaultName = 'kv-${workload}-${environment}-${regionCode}-${unique}'
```

`uniqueString` is a hash, not a random: the same subscription and environment
always produce the same suffix, so redeploying does not create a second
resource. Seed it with the **subscription id and the environment** — seeding it
with `resourceGroup().id` works too, but then the name changes if the resource
group is ever recreated, which is exactly when you least want a new Key Vault.

### 3.2 Names that cannot take hyphens

Storage accounts and container registries allow lowercase alphanumerics only,
3–24 and 5–50 characters respectively. Drop the separators and keep the order:

```
steedeveus<unique>         storage account
creesharedeus<unique>      container registry (one per subscription, env = shared)
```

There is **one container registry**, shared by every environment and living in
`rg-ee-shared-eus` (settled in P0-058.6). CI builds an image once and dev,
staging and prod run that same digest — what reaches prod is byte-for-byte what
was tested, and there is one ~$5/month registry instead of three. Its suffix is
seeded with `'shared'` as the environment, like any other §3.1 name. A per-env
registry becomes worth it only if prod moves to its own subscription (§1,
Phase 7); then images are promoted with `az acr import`, which keeps the digest.

### 3.3 Length limits that actually bite

| Resource | Limit | Charset | Notes |
|---|---|---|---|
| Key Vault | 3–24 | alphanumeric + `-` | Must start with a letter. No consecutive hyphens. `kv-ee-prod-eus-abcde` = 20, fits. |
| Storage account | 3–24 | lowercase alphanumeric | No hyphens at all. |
| Container registry | 5–50 | alphanumeric | No hyphens at all. |
| PostgreSQL Flexible Server | 3–63 | lowercase alphanumeric + `-` | Globally unique within the region. |
| Service Bus namespace | 6–50 | alphanumeric + `-` | Must start with a letter, end alphanumeric. |
| Azure Managed Redis | 1–60 | alphanumeric + single `-` | Becomes `<name>.<region>.redis.azure.net`, so it takes the §3.1 suffix. |
| Container App | 2–32 | lowercase alphanumeric + `-` | The tightest limit in the stack — keep role names short. |

Check the limit **before** adding a role segment. `ca-ee-migrations-prod-eus-001`
is 29 characters and fits; one more word would not.

### 3.4 Things that are not Azure resources

Cloudflare R2 buckets ([ADR-0002](./adr/0002-enterprise-saas-reframe.md)) and
the Cloudflare DNS zone are outside this scheme. R2 buckets follow
`ee-storage-<env>` (as already fixed by P0-013's `ee-storage-dev`). Keep them
recognisable, but do not force an Azure pattern onto a non-Azure service.

---

## 4. Resource type abbreviations

Azure's Cloud Adoption Framework abbreviations, so the names read the same way
as every other Azure estate and every doc you will search.

| Resource | Abbrev | First used by |
|---|---|---|
| Resource group | `rg` | P0-057.1 |
| Key Vault | `kv` | P0-057.2 |
| Log Analytics workspace | `log` | P0-057.3 |
| Application Insights | `appi` | P0-057.4 |
| PostgreSQL Flexible Server | `psql` | P0-058.1 |
| Container Apps environment | `cae` | P0-058.2 |
| Container App | `ca` | P0-058.3 |
| Service Bus namespace | `sbns` | P0-058.4 |
| Service Bus topic | `sbt` | P0-058.4 |
| Service Bus subscription | `sbts` | P0-058.4 |
| Azure Managed Redis | `amr` | P0-058.5 ([ADR-0008](./adr/0008-azure-managed-redis.md)) |
| User-assigned managed identity | `id` | P0-062.1 |
| Container registry | `cr` | P0-058.6 (shared, §3.2) |
| Storage account | `st` | diagnostics only — blobs live in R2 |
| SignalR Service | `sigr` | Phase 3 |

Adding a resource type? Take the abbreviation from the CAF list rather than
inventing one, and add a row here in the same change.

---

## 5. Tags

Every resource carries these five. The resource group carries them too — Azure
does **not** inherit tags from a resource group to its resources, so each module
takes a `tags` parameter and applies it. `main.<env>.bicep` builds the object
once and passes it down.

The rule is enforced by the compiler: type every module's `tags` parameter as
`requiredTags` from [`infra/types.bicep`](../infra/types.bicep). A missing
required tag, or an `env` outside `dev`/`stg`/`prod`/`shared`, then fails `bicep build`.

| Tag | Example | Why it exists |
|---|---|---|
| `env` | `dev` | Every filter, every cost view, every teardown script starts here. |
| `workload` | `early-eagles` | So a second workload in the same subscription is separable later. |
| `owner` | `razibur` | Who to ask. Solo today; a team address later. |
| `costCenter` | `platform` | Splits platform spend from a future per-customer dedicated deployment (Phase 7). |
| `managedBy` | `bicep` | Marks the resource as code-owned. Anything **without** this tag was created by hand and is a bug — that is the whole point of having it. |

Optional, where meaningful:

| Tag | Example | When |
|---|---|---|
| `dataClassification` | `confidential` | On anything storing member data (Postgres, backups, R2). Drives review scope. |
| `criticality` | `high` | On prod data-plane resources, to prioritise an incident. |

**Do not tag with anything that changes on every deploy** — no git SHA, no
timestamp, no build number. A mutable tag makes every deployment report a diff
on every resource, which trains you to ignore diffs, which is how a real change
slips past. Revision identity belongs on the Container App revision, which Azure
versions natively.

```bicep
param tags object = {
  env: environment
  workload: 'early-eagles'
  owner: 'razibur'
  costCenter: 'platform'
  managedBy: 'bicep'
}
```

---

## 6. Deployment naming

Subscription-scope deployments are listed by name in the portal and by
`az deployment sub list`, so the name is the audit trail:

```
az deployment sub create \
  --name ee-dev-20260921-1430 \
  --location eastus \
  --template-file infra/main.dev.bicep \
  --parameters infra/main.dev.bicepparam
```

Pattern: `ee-<env>-<yyyyMMdd>-<HHmm>`. Unique per run, sorts chronologically,
and says what it touched. In GitHub Actions, generate it from the run
timestamp — never let it default, or every deploy overwrites the previous
deployment record and the history is gone.

---

## 7. SKU and cost guardrails

The envelope is **$50–80/month through Phase 1** ([Plan.md](../Plan.md) §1).
Dev and staging are sized to be cheap; prod is sized to be correct. Treat the
figures below as order-of-magnitude — verify current pricing in the Azure
pricing calculator for `eastus` before deploying, and confirm against the
billing alert from P0-003.

| Resource | dev / stg | prod | Note |
|---|---|---|---|
| PostgreSQL Flexible | Burstable `B1ms`, 32 GB, no HA | Burstable `B1ms`, 32 GB, no HA | The single largest line item. Prod runs the same size as dev under the cost posture below; move to General Purpose (+ HA) when a paying club's SLA needs it. |
| Container Apps | Consumption, scale-to-zero | Consumption, scale-to-zero (max 2) | Scale-to-zero keeps every environment inside the free monthly grant. Prod pays for it with a cold start of a few seconds after idle; `minReplicas: 1` is the fix when that starts to matter. |
| Service Bus | **Standard** (dev only for now) | **Standard**, not deployed yet | **Basic has no topics — only queues.** The outbox publishes to a topic (P0-050.2.2, P0-051), so Basic cannot work even in dev. ~$10/month per namespace: dev has one; staging and prod get theirs when the API runs there (P0-086 / P0-087). |
| Redis | **Not deployed yet**; then Azure Managed Redis `Balanced_B0`, HA off | Not deployed yet; then `Balanced_B1`, HA on | Nothing uses Redis yet, so no environment pays ~$13/month for it (ADR-0008 allows leaving it out). Add the `redis` block to an environment's `sizing` when a feature needs a cache or locks. Azure Cache for Redis can no longer be created — [ADR-0008](./adr/0008-azure-managed-redis.md). |
| Log Analytics | Pay-as-you-go, **daily cap** | Pay-as-you-go, daily cap | Set the cap. Ingestion is the classic runaway bill — a logging loop can outspend the database in a day. |
| Key Vault | Standard | Standard | Cost is per operation and negligible. Soft-delete and purge protection stay **on** in every environment. |
| Container registry | Basic (one, shared) | — | ~$5/month, 10 GB included. Geo-replication, private endpoints and retention policies are Premium-only and not needed before Phase 7. Admin user and anonymous pull stay **off**. |

**Cost posture (2026-10-06): as close to free as possible.** Every environment
runs the cheapest configuration that works, and anything nothing uses yet is left
out instead of provisioned "for later". Optional resources (Redis, Service Bus)
are present in an environment only when its `sizing` object includes them
([`environment.bicep`](../infra/environment.bicep)), so turning one on is a
one-block change in `main.<env>.bicep`. Rough steady state: Postgres ~$16/month
per deployed environment (compute + 32 GB), Service Bus ~$10 (dev only), the shared
registry ~$5; Container Apps, Key Vault and Log Analytics stay within free grants
at this traffic. That is roughly **$47/month for dev + prod + registry**, about
$16 more whenever staging is deployed, so deploy staging only when it's needed.
Moving up a tier is a deliberate decision recorded here.

Turning dev off overnight is not worth the complexity at this scale — scale-to-zero
and burstable tiers already do most of that work.

---

## 8. Checklist — adding a resource

Before opening a PR that adds infrastructure:

- [ ] Name follows §3 and uses the §4 abbreviation (added a row there if new).
- [ ] Name fits the length/charset limit for its type (§3.3) **in `prod`**, which
      is the longest environment code.
- [ ] Globally-unique types carry the `uniqueString` suffix (§3.1).
- [ ] Module takes `location` and `tags` parameters and hard-codes neither.
- [ ] Module takes the SKU as a parameter; `main.<env>.bicep` picks the value.
- [ ] Required tags applied (§5), with `tags` typed as `requiredTags`.
- [ ] No secret in any `.bicepparam`.
- [ ] `az bicep build` succeeds with no warnings.
- [ ] Cost impact stated in the PR description against the §7 table.

---

## 9. Open decisions

These are deliberately unresolved and should be settled by the task that first
needs them, not guessed at now.

| Decision | Blocked until | Note |
|---|---|---|
| Confirm `eastus` as primary region | P0-059.3 (first real deploy) | §2. Cheap to change before anything is provisioned. |
| Separate prod subscription | Phase 7 | §1. Driven by an enterprise customer requirement, not by engineering taste. |
| Postgres HA topology in prod | First paying club with an SLA | **Settled for now (P0-061): no HA, Burstable**, under the §7 cost posture. Zone-redundant roughly doubles the database cost. Revisit with real numbers; it is a `sizing` change in `main.prod.bicep`, not a module change. |
| Private networking (VNet + private endpoints) | Phase 7 | Public endpoints with firewall rules are the Phase 0 position. Private endpoints add cost and a VPN/bastion requirement for local admin access. |

---

*Keep this document current: when a naming rule, tag, region or SKU changes,
update it in the same change as the Bicep. See [PROGRESS.md](../PROGRESS.md) for
live task status.*

# Runbook — Deploy infrastructure (Bicep)

> **How an environment gets to Azure.** Everything is Bicep under [`infra/`](../../infra);
> nothing is clicked in the portal. This runbook takes an environment from nothing to an API
> running its real image against Postgres, and covers redeploys, token rotation and teardown.
> The *why* behind each resource is in the
> [developer walkthrough §12](../developer-walkthrough.md#12-infrastructure--bicep-modules); this is the
> *how*.

Task IDs: **P0-063** (this runbook); the deploy itself is **P0-059.3** (sandbox smoke test),
P0-086 (staging) and P0-087 (prod). See [PROGRESS.md](../../PROGRESS.md).

---

## 1. Which entry point, and when

| Entry point | Deploys to | When |
|---|---|---|
| [`main.dev.bicep`](../../infra/main.dev.bicep) | `rg-ee-sandbox-eus` by default (`EE_SANDBOX=true`) | Throwaway smoke test of the templates. Dev itself runs on your laptop ([ADR-0009](../adr/0009-local-dev-no-cloud-dev-environment.md)); there is no standing cloud dev. |
| [`main.staging.bicep`](../../infra/main.staging.bicep) | `rg-ee-stg-eus` | On demand, before a prod promote. |
| [`main.prod.bicep`](../../infra/main.prod.bicep) | `rg-ee-prod-eus` | Manual promote only. |

All three wire the same [`environment.bicep`](../../infra/environment.bicep), so they differ only in
sizing and in what's switched on (Service Bus is in dev only; Redis is nowhere, see
[infra-conventions §7](../infra-conventions.md#7-sku-and-cost-guardrails)).

## 2. Prerequisites (once per machine / operator)

- **Azure CLI** with Bicep (`az bicep version`; templates are built with 0.47).
- **psql** (any 16+ client) for the Postgres steps.
- **.NET 10 SDK + `dotnet ef`** for migrations: `dotnet tool restore` from `apps/api` (the manifest pins `dotnet-ef` 10.0.11), then run it as `dotnet tool run dotnet-ef` from there, or install it globally.
- **Permissions on the subscription:** *Owner*, or *Contributor* + *User Access Administrator*.
  Contributor alone fails: the templates create role assignments (the API identity's Key Vault
  and Service Bus access).
- Signed in to the right subscription:

```pwsh
az login
az account set --subscription "<subscription name or id>"
az account show --query "{name:name, id:id}" -o table
```

## 3. Environment variables

The `.bicepparam` files commit only values that are the same for everyone. Anything tied to a
person, a machine or a particular deploy comes from the environment, and a missing *required*
one fails before anything is sent to Azure.

| Variable | Required | Default | Meaning |
|---|---|---|---|
| `EE_PG_ADMIN_OBJECT_ID` | yes | — | Object ID of the Postgres Entra administrator (you). |
| `EE_PG_ADMIN_NAME` | yes | — | That admin's UPN; also the psql user name. |
| `EE_OPERATOR_IP` | no | none | Your public IP, let through the Postgres firewall for psql and migrations. |
| `EE_SANDBOX` | no (dev only) | `true` | `false` stands up a real cloud dev environment. Rarely what you want. |
| `EE_API_IMAGE` | no | public placeholder | The API image, e.g. `ghcr.io/razibur/early-eagles-api:sha-<commit>` (tags from `api-image.yml`). |
| `EE_API_PORT` | no | `80` | Port that image listens on (the placeholder serves 80; .NET images default to 8080). |
| `EE_PG_ALLOW_AZURE_SERVICES` | no | `false` | `true` opens the Postgres firewall to Azure-hosted services, which the API needs once it really connects (Consumption apps have no fixed outbound IP). Entra auth still gates the database. |

```pwsh
$env:EE_PG_ADMIN_OBJECT_ID = az ad signed-in-user show --query id -o tsv
$env:EE_PG_ADMIN_NAME      = az ad signed-in-user show --query userPrincipalName -o tsv
$env:EE_OPERATOR_IP        = (Invoke-RestMethod https://api.ipify.org)
```

## 4. Preflight — build, lint, what-if

Set the environment once and reuse it in every command below:

```pwsh
$envName = 'staging'                    # dev | staging | prod
$code    = @{ dev='dev'; staging='stg'; prod='prod' }[$envName]
```

```pwsh
az bicep build        --file infra/main.$envName.bicep   --stdout > $null
az bicep lint         --file infra/main.$envName.bicep
az bicep build-params --file infra/main.$envName.bicepparam --stdout > $null
```

All three must be silent apart from the "new Bicep release" notice. A `BCP427` from
`build-params` means a required `EE_*` variable is missing.

Then ask Azure what would change. **Read the output.** A deploy you didn't preview is a deploy
you didn't review:

```pwsh
az deployment sub what-if `
  --location eastus `
  --template-file infra/main.$envName.bicep `
  --parameters infra/main.$envName.bicepparam
```

Expect creates on a first deploy, and on a redeploy only the changes you meant. If tags, SKUs or
anything you didn't touch shows as modified, stop and find out why.

## 5. Deploy

The deployment name is the audit trail, so never let it default
([infra-conventions §6](../infra-conventions.md#6-deployment-naming)):

```pwsh
$name = "ee-$code-$(Get-Date -Format 'yyyyMMdd-HHmm')"
az deployment sub create `
  --name $name `
  --location eastus `
  --template-file infra/main.$envName.bicep `
  --parameters infra/main.$envName.bicepparam
```

A first deploy takes ~10–15 minutes, mostly Postgres. Then capture the outputs that the next
steps use:

```pwsh
$out = az deployment sub show --name $name --query properties.outputs -o json | ConvertFrom-Json
$rg         = $out.resourceGroupName.value
$pgHost     = $out.postgresFqdn.value
$pgDb       = $out.postgresDatabaseName.value
$apiRole    = $out.apiIdentityName.value
$apiOid     = $out.apiIdentityPrincipalId.value
$kvName     = ([Uri]$out.keyVaultUri.value).Host.Split('.')[0]
$apiFqdn    = $out.apiFqdn.value
```

At this point the app runs the public placeholder image and touches nothing else, so a healthy
first deploy is `curl https://$apiFqdn` returning the placeholder page.

## 6. First deploy of an environment — one-time steps

Do these once per environment, in order, before deploying the real API image. Steps 6.1–6.3 need
`EE_OPERATOR_IP` set on the deploy, so the firewall lets you in.

### 6.1 Schema — run the migrations as the Entra admin

Azure Postgres accepts Entra tokens only. A token lasts about an hour and is the password:

```pwsh
$token = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
$env:ConnectionStrings__Postgres = "Host=$pgHost;Database=$pgDb;Username=$env:EE_PG_ADMIN_NAME;Password=$token;SslMode=VerifyFull"
cd apps/api      # dotnet-ef is a local tool whose manifest is here
dotnet ef database update `
  --project src/EarlyEagles.Infrastructure `
  --startup-project src/EarlyEagles.Infrastructure
cd ../..
Remove-Item Env:ConnectionStrings__Postgres
```

Later schema changes follow [`db-migrate.md`](./db-migrate.md) (reviewed idempotent script, expand
before contract).

Migrations always run as the admin, never as the API: the API's role has no DDL (6.3). Whoever
runs migrations owns the tables, and the default privileges in 6.3 are tied to that role.

### 6.2 The API's Postgres role

Bicep can't create a role *inside* Postgres, so it's SQL. This script runs against the
**`postgres`** database:

```pwsh
$env:PGPASSWORD = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
psql "host=$pgHost dbname=postgres user=$env:EE_PG_ADMIN_NAME sslmode=require" `
  -v role_name=$apiRole -v principal_id=$apiOid `
  -f infra/sql/api-role/01-create-principal.sql
```

`principal_id` is the identity's **object** ID (`apiIdentityPrincipalId`), not its client ID.
Re-running prints `already exists; skipped`.

### 6.3 The API role's grants

This one runs against the **application** database, as the same admin who ran the migrations:

```pwsh
psql "host=$pgHost dbname=$pgDb user=$env:EE_PG_ADMIN_NAME sslmode=require" `
  -v role_name=$apiRole `
  -f infra/sql/api-role/02-grants.sql
Remove-Item Env:PGPASSWORD
```

The API gets row access only: no DDL, and SELECT/INSERT only on `audit_events`. Tables later
migrations add are covered by default privileges, so this is a one-time step unless migrations
start running as a different role (then re-run with `-v migration_owner=<role>`). Details in
[walkthrough §12.19](../developer-walkthrough.md#1219-the-identitys-postgres-role--infrasqlapi-role-p0-06222).

### 6.4 The `ghcr.io` pull token

The image is private, and Container Apps can't use a managed identity with GHCR, so the app pulls
with a token from Key Vault ([ADR-0010](../adr/0010-ghcr-instead-of-acr.md)).

1. On GitHub, create a **classic** personal access token with **only `read:packages`** and a
   one-year expiry. GitHub Packages doesn't accept fine-grained tokens.
2. Give yourself write access to the vault's secrets. The vault uses RBAC, and Owner includes
   no data access:

   ```pwsh
   $kvId = az keyvault show -g $rg -n $kvName --query id -o tsv
   az role assignment create --role "Key Vault Secrets Officer" --scope $kvId `
     --assignee (az ad signed-in-user show --query id -o tsv)
   ```

   It can take a few minutes to take effect.
3. Store the token. Read it from a file so it stays out of your shell history:

   ```pwsh
   az keyvault secret set --vault-name $kvName --name ghcr-pull-token `
     --file token.txt --expires (Get-Date).AddYears(1).ToString('yyyy-MM-ddT00:00:00Z')
   Remove-Item token.txt
   ```

   The secret name is the `ghcrPullTokenSecretName` output.

## 7. Deploy the real API image

Once §6 is done, point the environment at a real image and open the firewall to the app. CI
publishes one per green `main` commit (`.github/workflows/api-image.yml`, P0-084), tagged
`sha-<full commit>`; the run's summary prints the exact reference. Always deploy a `sha-` tag, never
the moving `main` tag, so the deployed image is the one you tested.

```pwsh
$env:EE_API_IMAGE = 'ghcr.io/razibur/early-eagles-api:sha-<commit>'
$env:EE_API_PORT  = '8080'
$env:EE_PG_ALLOW_AZURE_SERVICES = 'true'
```

Then repeat §4 (what-if should show only the Container App, plus the Postgres firewall rule)
and §5. Because the image is on `ghcr.io/`, the app gets the registry block pointing at
`ghcr-pull-token`, and it signs in to Postgres with its identity. No password is set anywhere.

## 8. Verify

| Check | Command | Expect |
|---|---|---|
| Deployment succeeded | `az deployment sub show -n $name --query properties.provisioningState -o tsv` | `Succeeded` |
| App is running | `az containerapp show -g $rg -n ca-ee-api-$code-eus-001 --query "properties.runningStatus" -o tsv` | `Running` |
| Revision healthy | `az containerapp revision list -g $rg -n ca-ee-api-$code-eus-001 --query "[].{name:name, health:properties.healthState, active:properties.active}" -o table` | the active revision `Healthy` |
| Identity attached | `az containerapp show -g $rg -n ca-ee-api-$code-eus-001 --query identity.userAssignedIdentities` | one `id-ee-api-<env>-eus` entry |
| API answers | `curl -i https://$apiFqdn/health` (real image) or `https://$apiFqdn` (placeholder) | `200` |
| Database reachable | `curl -i https://$apiFqdn/ready` (real image) | `200` |

A revision stuck pulling usually means the token step (6.4) is missing or the grant hasn't
taken effect yet. The logs say which:

```pwsh
az containerapp logs show -g $rg -n ca-ee-api-$code-eus-001 --type system --tail 50
```

## 9. Routine redeploys

Set the `EE_*` variables, then §4 (what-if) and §5 (deploy, with a new timestamped name). §6 is
not repeated. Every deploy is idempotent: role assignments have deterministic names, and the SQL
scripts aren't part of it.

## 10. Rotate the `ghcr.io` pull token

**Calendar it:** the token expires, and when it does, new revisions can't pull. Running replicas
keep going, but the next deploy or scale-out fails. Set a reminder a month before the expiry you
chose in 6.4.

1. Create a new classic token (`read:packages` only).
2. Store it as a new value of the same secret:
   ```pwsh
   az keyvault secret set --vault-name $kvName --name ghcr-pull-token `
     --file token.txt --expires (Get-Date).AddYears(1).ToString('yyyy-MM-ddT00:00:00Z')
   Remove-Item token.txt
   ```
3. Nothing to redeploy. The app's reference has no version, so Container Apps picks up the new
   value, and any new revision reads the latest.
4. Once a new revision has pulled successfully, delete the old token on GitHub.

Check when the current token expires:

```pwsh
az keyvault secret show --vault-name $kvName --name ghcr-pull-token --query attributes.expires -o tsv
```

## 11. Tear down a sandbox

The sandbox (`main.dev.bicep` with `EE_SANDBOX=true`) is meant to be deleted after the smoke
test:

```pwsh
az group delete --name rg-ee-sandbox-eus --yes
```

Only ever delete `rg-ee-sandbox-eus` this way. Staging and prod are not torn down by this
runbook.

⚠ **The sandbox vault stays reserved for 7 days.** Purge protection keeps a deleted vault's name
for its soft-delete window (7 days for the sandbox, 90 for real environments), and the sandbox
reuses its name. Redeploying the sandbox inside that window fails with "name already in use".
Wait it out, or recover the vault. If its resource group was deleted, create the group again
first, then run `az keyvault recover --name <vault>`. See
[walkthrough §12.3](../developer-walkthrough.md#123-keyvaultbicep-p0-0572) and the troubleshooting table.

## 12. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `build-params` fails with `BCP427` | a required `EE_*` variable isn't set | set `EE_PG_ADMIN_OBJECT_ID` / `EE_PG_ADMIN_NAME` (§3) |
| `build-params` fails with `BCP338` on `apiTargetPort` | `EE_API_PORT` isn't a number | fix the variable |
| Deploy fails: `AuthorizationFailed` on `roleAssignments/write` | deploying as Contributor | Owner, or add User Access Administrator (§2) |
| Deploy fails: `PrincipalNotFound` | identity not yet replicated | shouldn't happen (`principalType` is set); redeploy |
| Revision can't pull: Key Vault secret access denied | 6.4 skipped, or the identity's grant is still propagating | store the token (6.4); wait a few minutes and redeploy |
| Revision can't pull: `unauthorized` from ghcr.io | token expired, revoked, or missing `read:packages` | rotate (§10) |
| psql / migrations time out | your IP isn't in the firewall | redeploy with `EE_OPERATOR_IP` set (or your IP changed) |
| psql: `password authentication failed` | token expired (they last ~1 hour), or wrong user | fetch a new token; `user` must be the admin UPN |
| API logs `permission denied for table …` | 6.3 not run, or a table created by a role other than the migration owner | run 6.3 (with `-v migration_owner=` if needed) |
| API can't reach Postgres at all | `EE_PG_ALLOW_AZURE_SERVICES` not `true` on the deploy | redeploy with it set (§7) |
| Key Vault redeploy fails: name already in use | soft-deleted, purge-protected vault | §11 |

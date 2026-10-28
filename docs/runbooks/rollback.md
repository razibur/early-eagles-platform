# Runbook — rolling back (P0-097.2)

**App first, database never backwards.** Migrations are expand-first ([`db-migrate.md`](./db-migrate.md)),
so the previous app version runs on the current schema. Nothing runs a down migration against
staging or prod.

## 1. Roll the API back to the previous image

Every green `main` commit has an image `ghcr.io/razibur/early-eagles-api:sha-<commit>` (P0-084).

```pwsh
# Find what is running and what ran before
az containerapp revision list -n ca-ee-api-<env>-eus-001 -g rg-ee-<env>-eus `
  --query "[].{name:name, image:properties.template.containers[0].image, active:properties.active, created:properties.createdTime}" -o table

# Fastest: point the app at the previous sha (Single revision mode replaces the revision)
az containerapp update -n ca-ee-api-<env>-eus-001 -g rg-ee-<env>-eus `
  --image ghcr.io/razibur/early-eagles-api:sha-<previous-commit>
```

Then make it durable: set `EE_API_IMAGE` to the same tag and redeploy through
[`deploy-infra.md` §5](./deploy-infra.md#5-deploy), or the next infra deploy reverts it.

**Verify:** `/health` reports `1.0.0+<previous-commit>`, `/ready` returns 200.

## 2. Data problems

| Situation | Do |
|---|---|
| A migration added something the old version ignores | Nothing; leave it. Fix forward. |
| Data was damaged | Restore to a **new** server at a time before the damage, copy the affected rows back, keep the live server running: `az postgres flexible-server restore -g rg-ee-<env>-eus -n <new-server> --source-server <live-server> --restore-time "<UTC ISO8601>"` (7-day window). |
| `audit_events` | Append-only (triggers reject UPDATE/DELETE/TRUNCATE). Never "roll back" audit rows; compensating actions are new events. |

## 3. Afterwards

Record what was rolled back, from which sha to which, and why, for the incident write-up.

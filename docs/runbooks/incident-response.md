# Runbook — incident response (P0-097.4)

For anything that stops clubs using the platform, or might expose one club's data to another.
Today there is one responder, the project owner. The roles below are written so a second person can
step in without re-reading the codebase.

## 1. Severity

| Sev | Meaning | Examples | Respond |
|---|---|---|---|
| **1** | Data exposure, or everyone down | one tenant can see another's data; API down for all tenants; data loss | immediately, any hour |
| **2** | Major feature down for many | sign-in broken; Sunday RSVP failing; `/ready` failing on all replicas | within 1 hour, waking hours |
| **3** | Degraded or one tenant affected | slow pages; one club's data wrong; background events delayed | next working day |

**A suspected cross-tenant leak is always Sev 1**, even before it's confirmed.

## 2. First 15 minutes

1. **Write down the start time** (UTC) and what you saw. Keep notes as you go; they become the
   write-up.
2. **Is it live?** `/health` (process up) and `/ready` (dependencies) on the affected environment.
   `/ready` names the failing check (`postgres`, `servicebus`).
3. **Did something just change?** The last deploy (`az containerapp revision list …`, see
   [`deploy.md`](./deploy.md)) and the last migration (`__EFMigrationsHistory`). If a change
   started it, **roll back first** ([`rollback.md`](./rollback.md)) and investigate after.
4. **Communicate.** Tell affected clubs' admins what's wrong and when you'll update next (WhatsApp
   for Tenant 0 today; the status page once P0-093 exists).

## 3. Where to look

Logs and traces carry `TenantId` / `TenantSlug` (logs) and `tenant.id` / `tenant.slug` (traces), so
everything below can be narrowed to one club (P0-089.2, P0-090).

```kusto
// Failed requests, last hour, by club (App Insights, workspace-based)
AppRequests
| where TimeGenerated > ago(1h) and Success == false
| summarize failures = count() by tostring(Properties["tenant.slug"]), ResultCode, Name
| order by failures desc

// Exceptions with their tenant
AppExceptions
| where TimeGenerated > ago(1h)
| project TimeGenerated, ProblemId, OuterMessage, tenant = tostring(Properties["TenantSlug"]), OperationId

// Everything one request did (paste an OperationId from above)
union AppRequests, AppDependencies, AppTraces, AppExceptions
| where OperationId == "<operation id>"
| order by TimeGenerated asc

// Container stdout (JSON lines) — crashes before telemetry starts, startup failures
ContainerAppConsoleLogs
| where TimeGenerated > ago(1h)
| project TimeGenerated, RevisionName, Log
| order by TimeGenerated desc
```

The exact table names are confirmed on the first real deploy (P0-059.3). If `ContainerAppConsoleLogs`
is empty, look for the same data in `ContainerAppConsoleLogs_CL`.

| Symptom | Likely cause | First check |
|---|---|---|
| `/ready` → `postgres` Unhealthy | server down, firewall, expired/failed Entra token | Postgres server status; `Database unreachable` exception in AppExceptions |
| `/ready` → `servicebus` Unhealthy | namespace down, role assignment removed | Service Bus namespace health; *Data Sender* role on the identity |
| Outbox rows piling up | bus unreachable, or a poison message | `SELECT count(*), max(attempt_count) FROM outbox_messages WHERE processed_at IS NULL` |
| New revision never healthy | image won't start; probe path wrong | `ContainerAppConsoleLogs` for the revision; [`deploy-infra.md`](./deploy-infra.md) §12 |
| One club sees another's data | **Sev 1** | §4 now |

## 4. Suspected cross-tenant data exposure (Sev 1)

1. **Contain.** If the leak is through one endpoint, roll back to the last release before it. If you
   can't tell, take the API off the internet:
   `az containerapp ingress disable -n ca-ee-api-<env>-eus-001 -g rg-ee-<env>-eus` (undo with
   `az containerapp ingress enable … --type external --target-port 8080`). Container Apps can't scale
   to zero replicas maximum, so cutting ingress is the off switch. Downtime is better than continued
   exposure.
2. **Preserve evidence.** Don't edit or delete rows. `audit_events` can't be changed anyway. Export
   the relevant App Insights operations and audit rows.
3. **Scope it.** Which tenants, which records, from when to when. Traces carry `tenant.id`, and the
   audit log shows who did what.
4. **Fix with a test first.** Every leak gets a regression test in the tenant-isolation suite
   (`EarlyEagles.Tests.Integration`, see [`tenant-isolation-tests.md`](./tenant-isolation-tests.md))
   that fails before the fix.
5. **Notify** affected clubs' admins with what was exposed and what you did. Check legal notification
   duties for the jurisdictions involved.

## 5. Afterwards

Within a week, a short write-up in `docs/incidents/YYYY-MM-DD-<slug>.md`: timeline (UTC), impact (who,
what, how long), cause, what fixed it, and follow-up tasks with IDs. Blameless: describe what made the
mistake easy, not who made it.

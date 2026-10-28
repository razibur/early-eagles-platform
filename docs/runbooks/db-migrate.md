# Runbook — database migrations (P0-097.3)

How schema changes reach a real database without downtime and without anyone being able to lose
data by accident. Read this before writing a migration, not after.

**Who runs them:** a person holding the Postgres **Entra admin** role, with a fresh token. Never the
API. The API's role (`id-ee-api-<env>-eus`) has no DDL by design ([`infra/sql/api-role/`](../../infra/sql/api-role/)),
the API never calls `Database.Migrate()`, and whoever runs migrations owns the tables, which the
API role's default privileges depend on. An automated runner (P0-088) will need its own identity
with that admin role; until then this is manual.

## 1. Writing a migration — rules

1. **Expand, then contract.** Every deploy must work with the schema before *and* after its
   migration, because the old app version keeps serving until the new revision is healthy, and a
   rollback (`rollback.md`) runs the old code against the new schema.

   | Change | Release 1 (expand) | Release 2+ (contract) |
   |---|---|---|
   | Add a column | add it **nullable** or with a default | make it required once every row and writer has it |
   | Rename a column | add the new column, write both, backfill | read the new one; then drop the old in a later release |
   | Drop a column / table | stop reading and writing it | drop it in a later release |
   | Change a type | add a new column of the new type, dual-write, backfill | switch reads, then drop the old one |

2. **One concept per migration**, named for what it does (`AddMemberPhone`, not `Update3`).
   Generate it from `apps/api` (the `dotnet ef` local tool only resolves there):

   ```pwsh
   cd apps/api
   dotnet ef migrations add AddMemberPhone --project src/EarlyEagles.Infrastructure --startup-project src/EarlyEagles.Infrastructure
   ```

3. **Tenant rules still apply.** A new domain table gets `tenant_id NOT NULL` with an FK to `tenants`,
   is covered by the global query filter (an `ITenantScoped` entity), and is added to the isolation
   tests. A backfill that crosses tenants is platform work: say so in the migration's comment.

4. **Hand-written SQL** (`migrationBuilder.Sql(...)`) for triggers, data backfills and indexes.
   Index on a large table: `CREATE INDEX CONCURRENTLY` can't run inside a transaction, and EF wraps
   each migration in one. Use `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY …", suppressTransaction: true)`
   in a migration of its own.

5. **Never edit or delete a migration that has run anywhere shared.** Fix forward with a new one.
   Down migrations exist for local work only; nothing runs `Down` against staging or prod.

6. **`audit_events` is append-only** at the storage layer: its triggers reject `UPDATE`, `DELETE` and
   `TRUNCATE`, even from the admin. A migration that needs to rewrite audit history is a design
   discussion, not a migration.

7. **Check locally** before any shared environment:

   ```pwsh
   docker compose up -d
   cd apps/api
   $env:ConnectionStrings__Postgres = "Host=localhost;Port=55432;Database=earlyeagles;Username=postgres;Password=postgres"
   dotnet ef database update --project src/EarlyEagles.Infrastructure --startup-project src/EarlyEagles.Infrastructure
   dotnet test tests/EarlyEagles.Tests.Integration      # applies every migration to a fresh Postgres
   ```

## 2. Applying to staging / prod

Do this **before** deploying the app version that needs the change (expand first).

1. **Generate the SQL and read it.** An idempotent script applies only what the target hasn't seen,
   so it's safe to run against any environment:

   ```pwsh
   cd apps/api
   dotnet ef migrations script --idempotent --output migrate.sql --project src/EarlyEagles.Infrastructure --startup-project src/EarlyEagles.Infrastructure
   ```

   Look for anything that locks or rewrites a big table (`ALTER COLUMN … TYPE`, a `NOT NULL` without
   a default, a non-concurrent index) and for any `DROP`. A `DROP` must belong to a contract step
   whose expand step is already live everywhere.

2. **Confirm a restore point exists.** Azure Postgres keeps **7 days** of point-in-time restore in
   every environment (`backupRetentionDays: 7`). Note the UTC time just before you apply.

3. **Apply as the Entra admin** (a token lasts about an hour). Firewall: deploy with `EE_OPERATOR_IP`
   set ([`deploy-infra.md` §6](./deploy-infra.md#6-first-deploy-of-an-environment--one-time-steps)).

   ```pwsh
   $env:PGPASSWORD = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv
   psql "host=$pgHost dbname=$pgDb user=$env:EE_PG_ADMIN_NAME sslmode=verify-full" -v ON_ERROR_STOP=1 -f migrate.sql
   Remove-Item Env:PGPASSWORD
   ```

   Don't add `-1` / `--single-transaction`: the script already opens one transaction per migration,
   and nesting them makes psql warn and leaves nothing applied. (`dotnet ef database update` with the
   token in the connection string, as in `deploy-infra.md` §6.1, applies the same migrations.)

4. **Verify:**

   ```sql
   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 5;
   ```

   Then the app's `/ready` stays 200, and the new revision's `/health` reports the expected commit.

5. **New tables are covered automatically:** the API role's default privileges grant it DML on
   tables the migration owner creates. Re-run [`02-grants.sql`](../../infra/sql/api-role/02-grants.sql)
   only if a migration was run as a *different* admin.

## 3. When a migration goes wrong

| Situation | Do |
|---|---|
| The script failed partway | Each migration is its own transaction, so a failed one rolled back by itself; the history table shows the last one that committed. Fix forward with a new migration and re-run the idempotent script. |
| It applied, but the app misbehaves | Roll the **app** back first ([`rollback.md`](./rollback.md)); with expand-only changes the old version runs fine on the new schema. Fix forward. |
| It destroyed or corrupted data | Point-in-time restore to a **new** server at the time noted in step 2, copy the affected rows back, and open an incident (`incident-response.md`, P0-097.4). Restores never overwrite the live server. |

## Verified

On 2026-10-27, against the compose Postgres: `dotnet ef migrations script --idempotent` produces a
script that applies all three migrations to an empty database and, run a second time, changes
nothing (0 errors or warnings, three history rows, both `audit_events` triggers present). Running it
with `--single-transaction` produces the nested-transaction warnings described above.

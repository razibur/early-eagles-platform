-- api-role/02-grants.sql — version 1 (P0-062.2.2, 2026-10-24)
--
-- Least-privilege grants for the API's Postgres role: read and write rows,
-- never change the schema. Migrations run separately as the server's admin
-- (the API never calls Migrate), so the API role needs no DDL at all.
--
-- Run against the APPLICATION database (earlyeagles), as the role that runs
-- migrations (on Azure: the Entra administrator), after the role exists
-- (01-create-principal.sql on Azure; a stand-in CREATE ROLE locally):
--
--   psql "host=... dbname=earlyeagles user=<migration owner> sslmode=require" \
--     -v role_name=id-ee-api-dev-eus \
--     -f infra/sql/api-role/02-grants.sql
--
-- Optional: -v migration_owner=<role> if migrations are run by a different
-- role than the one running this script (default: the current user). Default
-- privileges only apply to tables THAT role creates later.
--
-- Idempotent: GRANT / REVOKE / ALTER DEFAULT PRIVILEGES can all be re-run.
-- Re-run it after adding a table that needs the restrictions below; plain
-- new tables are covered by the default privileges.

\set ON_ERROR_STOP on

\if :{?role_name}
\else
  DO $$ BEGIN RAISE EXCEPTION 'missing -v role_name=<api role>'; END $$;
\endif
\if :{?migration_owner}
\else
  SELECT current_user AS migration_owner \gset
\endif

BEGIN;

-- Nobody but the owner creates objects in public. PUBLIC lost CREATE here
-- in Postgres 15; revoking again keeps the script correct on any server.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

SELECT current_database() AS db_name \gset
GRANT CONNECT ON DATABASE :"db_name" TO :"role_name";
GRANT USAGE ON SCHEMA public TO :"role_name";

-- Row access on every existing table. No TRUNCATE, REFERENCES or TRIGGER.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"role_name";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"role_name";

-- ...and on tables future migrations create.
ALTER DEFAULT PRIVILEGES FOR ROLE :"migration_owner" IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"role_name";
ALTER DEFAULT PRIVILEGES FOR ROLE :"migration_owner" IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO :"role_name";

-- audit_events is append-only. The migration's trigger already rejects
-- UPDATE / DELETE / TRUNCATE; withholding the privileges as well means a
-- dropped trigger alone doesn't reopen it.
REVOKE UPDATE, DELETE ON audit_events FROM :"role_name";

-- Migration history belongs to whoever migrates; the API doesn't read it.
REVOKE SELECT, INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" FROM :"role_name";

COMMIT;

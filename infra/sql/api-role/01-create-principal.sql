-- api-role/01-create-principal.sql — version 1 (P0-062.2.2, 2026-10-24)
--
-- Creates the Postgres role that the API's user-assigned managed identity
-- (id-ee-api-<env>-eus, P0-062.1) signs in as. Azure only: the
-- pgaadauth_* functions exist on Flexible Server, not in local Postgres.
--
-- Run ONCE per environment, after the server and identity are deployed:
--   - connected to the `postgres` database (not earlyeagles), because
--     pgaadauth_create_principal_with_oid only works there;
--   - signed in as the server's Entra administrator (postgresFlexible.bicep
--     `entraAdmin*`) with an Entra token as the password.
--
--   psql "host=<server>.postgres.database.azure.com dbname=postgres user=<entra-admin> sslmode=require" \
--     -v role_name=id-ee-api-dev-eus \
--     -v principal_id=<apiIdentityPrincipalId output> \
--     -f infra/sql/api-role/01-create-principal.sql
--
-- The role name must match the identity's name: the API sends it as
-- `Username` in its connection string (P0-062.2.3) and Postgres matches the
-- token's oid against the role created here. principal_id is the identity's
-- object (principal) ID, not its client ID.
--
-- Then run 02-grants.sql against the application database.

\set ON_ERROR_STOP on

\if :{?role_name}
\else
  DO $$ BEGIN RAISE EXCEPTION 'missing -v role_name=<identity name>'; END $$;
\endif
\if :{?principal_id}
\else
  DO $$ BEGIN RAISE EXCEPTION 'missing -v principal_id=<identity principal (object) id>'; END $$;
\endif

-- Idempotent: skip if the role already exists (re-running after a partial
-- deploy is normal). Arguments: name, object id, principal type, is_admin,
-- is_mfa. 'service' = managed identity / service principal; never admin.
SELECT CASE
         WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role_name')
           THEN 'role ' || :'role_name' || ' already exists; skipped'
         ELSE pgaadauth_create_principal_with_oid(
                :'role_name', :'principal_id', 'service', false, false)
       END AS result;

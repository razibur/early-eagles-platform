// P0-061 — production environment values for main.prod.bicep. Same shape as
// main.dev.bicepparam.
//
// Committed here: values that are the same for anyone deploying prod.
// Read from the environment: values tied to a person or a machine — the
// Postgres Entra admin and the operator's IP. Nothing in this file is a
// secret (rule 5); the admin's object ID is kept out only because it
// identifies a person and differs between the operator and CI.
//
//   $env:EE_PG_ADMIN_OBJECT_ID = az ad signed-in-user show --query id -o tsv
//   $env:EE_PG_ADMIN_NAME      = az ad signed-in-user show --query userPrincipalName -o tsv
//   $env:EE_OPERATOR_IP        = (Invoke-RestMethod https://api.ipify.org)   # optional
//
// A missing required variable fails `az bicep build-params` and the deploy
// before anything is sent to Azure.

using 'main.prod.bicep'

param owner = 'razibur'
param costCenter = 'platform'

// Prod logs a few MB a day at Tenant 0's traffic; 1 GB/day leaves room for a noisy
// debugging session and stops a logging loop at a few dollars a day
// (infra-conventions §7).
param logDailyQuotaGb = 1

param postgresEntraAdminObjectId = readEnvironmentVariable('EE_PG_ADMIN_OBJECT_ID')
param postgresEntraAdminPrincipalName = readEnvironmentVariable('EE_PG_ADMIN_NAME')
param postgresEntraAdminPrincipalType = 'User'

var operatorIp = readEnvironmentVariable('EE_OPERATOR_IP', '')
param postgresFirewallRules = empty(operatorIp)
  ? []
  : [
      {
        name: 'operator'
        startIpAddress: operatorIp
        endIpAddress: operatorIp
      }
    ]


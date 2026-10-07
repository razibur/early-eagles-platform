// P0-059.2 — values for main.dev.bicep. Since ADR-0009 dev runs locally, so this
// template is only deployed as a throwaway smoke test: EE_SANDBOX defaults to
// 'true' (rg-ee-sandbox-eus). Set it to 'false' only to stand up a cloud dev
// environment on purpose.
//
// Committed here: values that are the same for anyone deploying dev.
// Read from the environment: values tied to a person or a machine — the
// Postgres Entra admin and the operator's IP. Nothing in this file is a
// secret (rule 5); the admin's object ID is kept out only because it
// identifies a person and differs between the operator and CI.
//
//   $env:EE_PG_ADMIN_OBJECT_ID = az ad signed-in-user show --query id -o tsv
//   $env:EE_PG_ADMIN_NAME      = az ad signed-in-user show --query userPrincipalName -o tsv
//   $env:EE_OPERATOR_IP        = (Invoke-RestMethod https://api.ipify.org)   # optional
//   $env:EE_SANDBOX            = 'false'                                      # only for a standing cloud dev
//
// A missing required variable fails `az bicep build-params` and the deploy
// before anything is sent to Azure.

using 'main.dev.bicep'

param owner = 'razibur'
param costCenter = 'platform'

// Dev logs a few MB a day while idle; 1 GB/day leaves room for a noisy
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

param sandbox = bool(readEnvironmentVariable('EE_SANDBOX', 'true'))

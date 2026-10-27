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
//   $env:EE_API_IMAGE          = 'ghcr.io/razibur/early-eagles-api:<tag>'    # optional; default: public placeholder
//   $env:EE_API_PORT           = '8080'                                       # optional; default 80 (placeholder)
//   $env:EE_PG_ALLOW_AZURE_SERVICES = 'true'                                  # optional; needed once the API uses Postgres
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

// The API image and its port change per deploy (CI passes a tag, P0-084), so
// they come from the environment; the defaults are the public placeholder.
// A ghcr.io image needs its pull token in Key Vault first (P0-062.4).
param apiImage = readEnvironmentVariable('EE_API_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param apiTargetPort = int(readEnvironmentVariable('EE_API_PORT', '80'))

// The 0.0.0.0 rule: every Azure-hosted service can reach the Postgres
// endpoint (Entra auth still gates the database). Off until the API really
// connects, since Consumption Container Apps have no fixed outbound IP
// (infra-conventions §9).
param postgresAllowAzureServices = bool(readEnvironmentVariable('EE_PG_ALLOW_AZURE_SERVICES', 'false'))

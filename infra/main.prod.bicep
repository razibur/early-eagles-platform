// P0-061 — the production environment (infra-conventions §1, §6, §7). The
// wiring lives in environment.bicep; this file fixes the env code and the §7
// prod sizing.
//
// Sized for minimum cost, not for an SLA (infra-conventions §7, cost
// posture): Burstable Postgres with no HA, an API that scales to zero
// (a few seconds' cold start on the first request after idle), no Redis
// and no Service Bus until something running here needs them. Every one of
// these is a value below, so moving up a tier is a one-line change.
//
// Deploy (deployment name per §6 — never let it default). Prod deploys are
// manually approved (P0-087):
//   az deployment sub create --name ee-prod-<yyyyMMdd>-<HHmm> --location eastus //     --template-file infra/main.prod.bicep --parameters infra/main.prod.bicepparam

targetScope = 'subscription'

import { environmentSizing, firewallRule, topic } from 'environment.bicep'

@description('owner tag (infra-conventions §5).')
param owner string

@description('costCenter tag (infra-conventions §5).')
param costCenter string

@description('Log Analytics daily ingestion cap in GB (infra-conventions §7). No default on purpose — the number is chosen in the parameter file.')
@minValue(1)
param logDailyQuotaGb int

@description('Object ID of the Entra principal that administers Postgres.')
param postgresEntraAdminObjectId string

@description('Display name (UPN for a user) of the Postgres Entra admin; the psql user name.')
param postgresEntraAdminPrincipalName string

@description('Kind of principal the Postgres Entra admin is.')
@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
param postgresEntraAdminPrincipalType string

@description('Operator IPs allowed through the Postgres firewall, e.g. for psql.')
param postgresFirewallRules firewallRule[] = []

@description('Allow every Azure-hosted service through the Postgres firewall (the 0.0.0.0 rule). Needed once the API connects to Postgres — Consumption Container Apps have no fixed outbound IP — and only until private networking (infra-conventions §9).')
param postgresAllowAzureServices bool = false

@description('Service Bus topics and subscriptions. Chosen by P0-051.')
param serviceBusTopics topic[] = []

@description('API image. A ghcr.io image is pulled with the Key Vault-held token (P0-062.4, ADR-0010); anything else must be public. CI deploys the digest staging ran (P0-087).')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the API image listens on. The placeholder serves 80.')
param apiTargetPort int = 80

// infra-conventions §7, prod column.
var sizing environmentSizing = {
  logRetentionInDays: 30
  postgres: {
    skuName: 'Standard_B1ms'
    skuTier: 'Burstable'
    storageSizeGB: 32
    backupRetentionDays: 7
    highAvailabilityMode: 'Disabled'
    geoRedundantBackup: 'Disabled'
  }
  // No redis and no serviceBus until the API runs here (cost posture,
  // infra-conventions §7). Add the blocks back with P0-087.
  api: {
    cpu: '0.25'
    // Scale to zero: costs nothing idle, at the price of a cold start.
    minReplicas: 0
    maxReplicas: 2
  }
}

module env 'environment.bicep' = {
  name: '${deployment().name}-env'
  params: {
    environment: 'prod'
    sizing: sizing
    owner: owner
    costCenter: costCenter
    logDailyQuotaGb: logDailyQuotaGb
    postgresEntraAdminObjectId: postgresEntraAdminObjectId
    postgresEntraAdminPrincipalName: postgresEntraAdminPrincipalName
    postgresEntraAdminPrincipalType: postgresEntraAdminPrincipalType
    postgresFirewallRules: postgresFirewallRules
    postgresAllowAzureServices: postgresAllowAzureServices
    serviceBusTopics: serviceBusTopics
    apiImage: apiImage
    apiTargetPort: apiTargetPort
    sandbox: false
  }
}

@description('Resource group the environment was deployed into.')
output resourceGroupName string = env.outputs.resourceGroupName

@description('Key Vault URI.')
output keyVaultUri string = env.outputs.keyVaultUri

@description('Postgres server FQDN.')
output postgresFqdn string = env.outputs.postgresFqdn

@description('Application database name.')
output postgresDatabaseName string = env.outputs.postgresDatabaseName

@description('Service Bus fully qualified namespace.')
output serviceBusNamespace string = env.outputs.serviceBusNamespace

@description('Redis host name.')
output redisHostName string = env.outputs.redisHostName

@description('Redis TLS port.')
output redisPort int = env.outputs.redisPort

@description('Public FQDN of the API Container App.')
output apiFqdn string = env.outputs.apiFqdn

@description('Principal (object) ID of the API managed identity.')
output apiIdentityPrincipalId string = env.outputs.apiIdentityPrincipalId

@description('Name of the API managed identity; also its Postgres role (role_name for infra/sql/api-role/).')
output apiIdentityName string = env.outputs.apiIdentityName

@description('Client ID of the API managed identity (AZURE_CLIENT_ID on the app).')
output apiIdentityClientId string = env.outputs.apiIdentityClientId

@description('Key Vault secret that must hold the ghcr.io pull token before a ghcr.io image is deployed (runbook deploy-infra §6.4).')
output ghcrPullTokenSecretName string = env.outputs.ghcrPullTokenSecretName

@description('App Insights resource name.')
output appInsightsName string = env.outputs.appInsightsName

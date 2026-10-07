// P0-059.1 — the dev environment (infra-conventions §1, §6, §7). The wiring
// lives in environment.bicep (P0-060.1); this file fixes what makes it dev:
// the env code and the §7 dev sizing.
//
// Deploy (deployment name per §6 — never let it default):
//   az deployment sub create --name ee-dev-<yyyyMMdd>-<HHmm> --location eastus \
//     --template-file infra/main.dev.bicep --parameters infra/main.dev.bicepparam
//
// The parameter file (P0-059.2) supplies who and where — tag owner, the
// Postgres Entra admin, operator IPs, the Log Analytics daily cap.
//
// sandbox = true (P0-059.3) deploys a throwaway copy into rg-ee-sandbox-eus
// with its own globally unique names (see environment.bicep).

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

@description('API image. Must be publicly pullable until P0-062.4 wires the shared registry.')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the API image listens on. The placeholder serves 80.')
param apiTargetPort int = 80

@description('Deploy a throwaway copy into rg-ee-sandbox-eus with distinct globally unique names (P0-059.3).')
param sandbox bool = false

// infra-conventions §7, dev column.
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
  // No redis: nothing uses it yet, and it would be ~$13/month (cost posture,
  // infra-conventions §7). Add the block back when a feature needs it.
  serviceBus: {
    skuName: 'Standard'
  }
  api: {
    cpu: '0.25'
    minReplicas: 0
    maxReplicas: 1
  }
}

module env 'environment.bicep' = {
  name: '${deployment().name}-env'
  params: {
    environment: 'dev'
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
    sandbox: sandbox
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

@description('App Insights resource name.')
output appInsightsName string = env.outputs.appInsightsName

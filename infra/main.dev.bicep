// P0-059.1 — the dev environment: every module wired together at dev sizing
// (infra-conventions §1, §3, §6, §7).
//
// Deploy (deployment name per §6 — never let it default):
//   az deployment sub create --name ee-dev-<yyyyMMdd>-<HHmm> --location eastus \
//     --template-file infra/main.dev.bicep --parameters infra/main.dev.bicepparam
//
// What lives here vs the parameter file (P0-059.2):
//   - Here: the environment's identity (env code, region, names) and its
//     shape — the dev SKUs from §7, which are a property of "dev", not of a
//     particular deployment.
//   - Parameter file: who and where — tag owner, the Postgres Entra admin,
//     operator IPs, the Log Analytics daily cap, the image to run.
//
// Not here: the container registry (shared across environments,
// main.shared.bicep / P0-059.4), managed identity and role assignments
// (P0-062.*), Service Bus topics (P0-051 passes them via serviceBusTopics).
//
// sandbox = true (P0-059.3) deploys a throwaway copy into rg-ee-sandbox-eus.
// Its globally unique names get a different suffix: Key Vault purge
// protection keeps a deleted vault's name reserved for the retention window,
// so a sandbox that used the real dev names would block the real dev deploy.

targetScope = 'subscription'

import { requiredTags } from 'types.bicep'

@description('A Postgres firewall rule for an operator IP. Single addresses use the same start and end.')
type firewallRule = {
  name: string
  startIpAddress: string
  endIpAddress: string
}

@description('A Service Bus topic and its subscriptions (see modules/serviceBus.bicep).')
type topic = {
  name: string
  subscriptions: {
    name: string
    maxDeliveryCount: int?
  }[]
}

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

var environment = 'dev'
var location = 'eastus'
var regionCode = 'eus'

// §3.1 suffix, seeded with subscription + environment. The sandbox seeds
// differently so none of its global names can collide with real dev.
var unique = take(uniqueString(subscription().subscriptionId, sandbox ? '${environment}-sandbox' : environment), 5)

var resourceGroupName = sandbox ? 'rg-ee-sandbox-${regionCode}' : 'rg-ee-${environment}-${regionCode}'

// Postgres server names are DNS names with no suffix (§3), so the sandbox
// marks its own.
var postgresName = 'psql-ee-${environment}-${regionCode}-001${sandbox ? '-sbx' : ''}'

var tags requiredTags = {
  env: environment
  workload: 'early-eagles'
  owner: owner
  costCenter: costCenter
  managedBy: 'bicep'
}

// Nested deployments are named after the top-level one, so each run's
// history is kept rather than overwritten (§6).
var prefix = deployment().name

module rg 'modules/resourceGroup.bicep' = {
  name: '${prefix}-rg'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

module log 'modules/logAnalytics.bicep' = {
  name: '${prefix}-log'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'log-ee-${environment}-${regionCode}'
    location: location
    tags: tags
    skuName: 'PerGB2018'
    retentionInDays: 30
    dailyQuotaGb: logDailyQuotaGb
  }
}

module appi 'modules/appInsights.bicep' = {
  name: '${prefix}-appi'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: 'appi-ee-${environment}-${regionCode}'
    location: location
    tags: tags
    workspaceId: log.outputs.id
  }
}

module kv 'modules/keyVault.bicep' = {
  name: '${prefix}-kv'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'kv-ee-${environment}-${regionCode}-${unique}'
    location: location
    tags: tags
    skuName: 'standard'
    // A sandbox vault's name is freed 7 days after teardown instead of 90.
    softDeleteRetentionInDays: sandbox ? 7 : 90
  }
}

module psql 'modules/postgresFlexible.bicep' = {
  name: '${prefix}-psql'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: postgresName
    location: location
    tags: tags
    skuName: 'Standard_B1ms'
    skuTier: 'Burstable'
    storageSizeGB: 32
    backupRetentionDays: 7
    highAvailabilityMode: 'Disabled'
    entraAdminObjectId: postgresEntraAdminObjectId
    entraAdminPrincipalName: postgresEntraAdminPrincipalName
    entraAdminPrincipalType: postgresEntraAdminPrincipalType
    allowAzureServices: postgresAllowAzureServices
    firewallRules: postgresFirewallRules
  }
}

module sb 'modules/serviceBus.bicep' = {
  name: '${prefix}-sb'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'sbns-ee-${environment}-${regionCode}-${unique}'
    location: location
    tags: tags
    skuName: 'Standard'
    topics: serviceBusTopics
  }
}

module redis 'modules/redis.bicep' = {
  name: '${prefix}-redis'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'amr-ee-${environment}-${regionCode}-${unique}'
    location: location
    tags: tags
    skuName: 'Balanced_B0'
    highAvailability: false
  }
}

module cae 'modules/containerAppsEnv.bicep' = {
  name: '${prefix}-cae'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: 'cae-ee-${environment}-${regionCode}'
    location: location
    tags: tags
    workspaceId: log.outputs.id
  }
}

module api 'modules/containerApp.bicep' = {
  name: '${prefix}-api'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: 'ca-ee-api-${environment}-${regionCode}-001'
    location: location
    tags: tags
    environmentId: cae.outputs.id
    image: apiImage
    targetPort: apiTargetPort
    externalIngress: true
    cpu: '0.25'
    minReplicas: 0
    maxReplicas: 1
    // No env vars and no probes yet: the placeholder image reads no config
    // and serves only /. Endpoints, secrets (Key Vault references) and the
    // /health + /ready probes arrive with the real image and managed
    // identity (P0-062.*, P0-084).
  }
}

@description('Resource group the environment was deployed into.')
output resourceGroupName string = resourceGroupName

@description('Key Vault URI.')
output keyVaultUri string = kv.outputs.uri

@description('Postgres server FQDN.')
output postgresFqdn string = psql.outputs.fqdn

@description('Application database name.')
output postgresDatabaseName string = psql.outputs.databaseName

@description('Service Bus fully qualified namespace.')
output serviceBusNamespace string = sb.outputs.fullyQualifiedNamespace

@description('Redis host name.')
output redisHostName string = redis.outputs.hostName

@description('Redis TLS port.')
output redisPort int = redis.outputs.port

@description('Public FQDN of the API Container App.')
output apiFqdn string = api.outputs.fqdn

@description('App Insights resource name. The connection string is a secure output of the module and is deliberately not re-exported.')
output appInsightsName string = appi.outputs.name

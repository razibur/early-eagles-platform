// P0-060.1 — one environment: every per-environment module wired together
// (infra-conventions §1, §3, §6, §7). Called by main.dev.bicep,
// main.staging.bicep and (P0-061) main.prod.bicep, which each pass their env
// code and §7 sizing. Extracted from main.dev.bicep so the environments
// cannot drift apart in wiring — only in the values they pass.
//
// Not here: a container registry (images live on ghcr.io, ADR-0010), role
// assignments for the API identity (P0-062.2–.4), Service Bus topics (passed
// in by P0-051).
//
// sandbox = true deploys a throwaway copy into rg-ee-sandbox-eus. Its
// globally unique names get a different suffix: Key Vault purge protection
// keeps a deleted vault's name reserved for the retention window, so a
// sandbox that used an environment's real names would block its real deploy.

targetScope = 'subscription'

import { requiredTags } from 'types.bicep'

@description('A Postgres firewall rule for an operator IP. Single addresses use the same start and end.')
@export()
type firewallRule = {
  name: string
  startIpAddress: string
  endIpAddress: string
}

@description('A Service Bus topic and its subscriptions (see modules/serviceBus.bicep).')
@export()
type topic = {
  name: string
  subscriptions: {
    name: string
    maxDeliveryCount: int?
  }[]
}

@description('The §7 sizing of one environment. Each main.<env>.bicep fixes its own. Leaving out redis or serviceBus means that resource is not deployed in the environment.')
@export()
type environmentSizing = {
  logRetentionInDays: int
  postgres: {
    skuName: string
    skuTier: 'Burstable' | 'GeneralPurpose' | 'MemoryOptimized'
    storageSizeGB: int
    backupRetentionDays: int
    highAvailabilityMode: 'Disabled' | 'SameZone' | 'ZoneRedundant'
    geoRedundantBackup: 'Disabled' | 'Enabled'
  }
  redis: {
    skuName: 'Balanced_B0' | 'Balanced_B1' | 'Balanced_B3' | 'Balanced_B5'
    highAvailability: bool
  }?
  serviceBus: {
    skuName: 'Standard' | 'Premium'
  }?
  api: {
    cpu: '0.25' | '0.5' | '0.75' | '1' | '1.25' | '1.5' | '1.75' | '2'
    minReplicas: int
    maxReplicas: int
  }
}

@description('Environment code (infra-conventions §1).')
param environment 'dev' | 'stg' | 'prod'

@description('This environment\'s §7 sizing.')
param sizing environmentSizing

@description('owner tag (infra-conventions §5).')
param owner string

@description('costCenter tag (infra-conventions §5).')
param costCenter string

@description('Log Analytics daily ingestion cap in GB (infra-conventions §7).')
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

@description('Operator IPs allowed through the Postgres firewall.')
param postgresFirewallRules firewallRule[]

@description('Allow every Azure-hosted service through the Postgres firewall (the 0.0.0.0 rule).')
param postgresAllowAzureServices bool

@description('Service Bus topics and subscriptions.')
param serviceBusTopics topic[]

@description('API image.')
param apiImage string

@description('Port the API image listens on.')
param apiTargetPort int

@description('Deploy a throwaway copy into rg-ee-sandbox-eus with distinct globally unique names.')
param sandbox bool

var location = 'eastus'
var regionCode = 'eus'

// §3.1 suffix, seeded with subscription + environment. The sandbox seeds
// differently so none of its global names can collide with the real one.
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
    retentionInDays: sizing.logRetentionInDays
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
    skuName: sizing.postgres.skuName
    skuTier: sizing.postgres.skuTier
    storageSizeGB: sizing.postgres.storageSizeGB
    backupRetentionDays: sizing.postgres.backupRetentionDays
    highAvailabilityMode: sizing.postgres.highAvailabilityMode
    geoRedundantBackup: sizing.postgres.geoRedundantBackup
    entraAdminObjectId: postgresEntraAdminObjectId
    entraAdminPrincipalName: postgresEntraAdminPrincipalName
    entraAdminPrincipalType: postgresEntraAdminPrincipalType
    allowAzureServices: postgresAllowAzureServices
    firewallRules: postgresFirewallRules
  }
}

module sb 'modules/serviceBus.bicep' = if (sizing.?serviceBus != null) {
  name: '${prefix}-sb'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'sbns-ee-${environment}-${regionCode}-${unique}'
    location: location
    tags: tags
    skuName: sizing.?serviceBus.?skuName ?? 'Standard'
    topics: serviceBusTopics
  }
}

module redis 'modules/redis.bicep' = if (sizing.?redis != null) {
  name: '${prefix}-redis'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'amr-ee-${environment}-${regionCode}-${unique}'
    location: location
    tags: tags
    skuName: sizing.?redis.?skuName ?? 'Balanced_B0'
    highAvailability: sizing.?redis.?highAvailability ?? false
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

// The API's identity (P0-062.1). Its own module, not created by the app, so
// role assignments (P0-062.2–.4) can be granted before the app first starts.
module apiIdentity 'modules/userAssignedIdentity.bicep' = {
  name: '${prefix}-api-id'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    name: 'id-ee-api-${environment}-${regionCode}'
    location: location
    tags: tags
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
    cpu: sizing.api.cpu
    minReplicas: sizing.api.minReplicas
    maxReplicas: sizing.api.maxReplicas
    identity: {
      id: apiIdentity.outputs.id
      clientId: apiIdentity.outputs.clientId
    }
    // Password-less Postgres (P0-062.2.3): the API signs in with an Entra
    // token for its identity (P0-062.2.1). Username is the identity's name,
    // which is also the role infra/sql/api-role/ creates, so the two can't
    // drift. VerifyFull, not Require: Npgsql's Require encrypts without
    // checking the certificate; Azure's chains to a public root the image
    // already trusts. Nothing secret here, so plain env vars.
    // The placeholder image ignores these; no probes until the real image
    // (P0-084).
    env: [
      {
        name: 'Postgres__Authentication'
        value: 'Entra'
      }
      {
        name: 'ConnectionStrings__Postgres'
        value: 'Host=${psql.outputs.fqdn};Database=${psql.outputs.databaseName};Username=${apiIdentity.outputs.name};SslMode=VerifyFull'
      }
    ]
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

@description('Service Bus fully qualified namespace; empty when the environment has no Service Bus.')
output serviceBusNamespace string = sb.?outputs.?fullyQualifiedNamespace ?? ''

@description('Redis host name; empty when the environment has no Redis.')
output redisHostName string = redis.?outputs.?hostName ?? ''

@description('Redis TLS port; 0 when the environment has no Redis.')
output redisPort int = redis.?outputs.?port ?? 0

@description('Public FQDN of the API Container App.')
output apiFqdn string = api.outputs.fqdn

@description('Principal (object) ID of the API identity — for granting it access by hand or from another template.')
output apiIdentityPrincipalId string = apiIdentity.outputs.principalId

@description('Name of the API identity — also its Postgres role (role_name for infra/sql/api-role/).')
output apiIdentityName string = apiIdentity.outputs.name

@description('Client ID of the API identity (also set on the app as AZURE_CLIENT_ID).')
output apiIdentityClientId string = apiIdentity.outputs.clientId

@description('App Insights resource name. The connection string is a secure output of the module and is deliberately not re-exported.')
output appInsightsName string = appi.outputs.name

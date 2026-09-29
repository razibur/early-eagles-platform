// P0-058.1 — the per-environment PostgreSQL Flexible Server (infra-conventions §4, §7).
//
//   psql-ee-<env>-<region>-001
//
// The pool-model database: one server, one application database, tenant_id on
// every row. SKU, HA, storage and backup settings are parameters so the same
// module builds a Burstable no-HA dev server and a General Purpose prod server
// (infra-conventions §7). Whether prod runs zone-redundant HA is an open
// decision (§9) settled in main.prod.bicep, not here.
//
// Fixed in the module:
//   - Entra ID authentication only; password auth is disabled. No admin
//     password exists, so none can leak through a parameter file (rule 5).
//     Connect with an Entra token as the password:
//       az account get-access-token --resource-type oss-rdbms
//   - dataClassification: confidential is always added to the tags — this
//     server stores member data (infra-conventions §5).

import { requiredTags } from '../types.bicep'

@description('A public-access firewall rule. Single addresses use the same start and end.')
type firewallRule = {
  name: string
  startIpAddress: string
  endIpAddress: string
}

@description('Server name, e.g. psql-ee-dev-eus-001 (infra-conventions §3). Lowercase alphanumerics and hyphens.')
@minLength(3)
@maxLength(63)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5). dataClassification: confidential is added by the module.')
param tags requiredTags

@description('Compute SKU, e.g. Standard_B1ms (dev/stg) or Standard_D2ds_v5 (prod). Must belong to skuTier.')
param skuName string

@description('Compute tier. Burstable does not support high availability.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param skuTier string

@description('PostgreSQL major version. Matches the postgres:17 image used for local dev and Testcontainers.')
@allowed([
  '16'
  '17'
])
param postgresVersion string = '17'

@description('Storage in GB. Can grow later but never shrink.')
@minValue(32)
param storageSizeGB int

@description('Days of point-in-time-restore backups.')
@minValue(7)
@maxValue(35)
param backupRetentionDays int

@description('Geo-redundant backup copies. Can only be chosen at server creation.')
@allowed([
  'Enabled'
  'Disabled'
])
param geoRedundantBackup string = 'Disabled'

@description('High availability. Disabled for dev/stg; the prod choice is an open decision (infra-conventions §9). Not available on Burstable.')
@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param highAvailabilityMode string

@description('Object ID of the Entra ID user, group or service principal that administers the server.')
param entraAdminObjectId string

@description('Display name (UPN for a user) of the Entra ID administrator. Used as the psql user name.')
param entraAdminPrincipalName string

@description('Kind of principal the Entra ID administrator is.')
@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
param entraAdminPrincipalType string

@description('Name of the application database created on the server.')
param databaseName string = 'earlyeagles'

@description('Allow connections from any Azure-hosted service (the 0.0.0.0 rule). Covers every Azure customer, not just this subscription — only a stopgap until private networking (infra-conventions §9).')
param allowAzureServices bool = false

@description('Extra firewall rules, e.g. an operator IP for psql.')
param firewallRules firewallRule[] = []

@description('Maintenance day, 0 = Sunday. Default Tuesday — Sunday is club play day.')
@minValue(0)
@maxValue(6)
param maintenanceDayOfWeek int = 2

@description('Maintenance start hour in UTC. Default 07:00 UTC (03:00 US Eastern).')
@minValue(0)
@maxValue(23)
param maintenanceStartHour int = 7

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: name
  location: location
  tags: union(tags, { dataClassification: 'confidential' })
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    version: postgresVersion
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: subscription().tenantId
    }
    storage: {
      storageSizeGB: storageSizeGB
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: geoRedundantBackup
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    maintenanceWindow: {
      customWindow: 'Enabled'
      dayOfWeek: maintenanceDayOfWeek
      startHour: maintenanceStartHour
      startMinute: 0
    }
    // Public access with firewall rules is the Phase 0 position (infra-conventions §9).
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource admin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = {
  parent: server
  name: entraAdminObjectId
  properties: {
    principalName: entraAdminPrincipalName
    principalType: entraAdminPrincipalType
    tenantId: subscription().tenantId
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource azureServicesRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = if (allowAzureServices) {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Serial: the server rejects concurrent firewall-rule writes.
@batchSize(1)
resource extraRules 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = [
  for rule in firewallRules: {
    parent: server
    name: rule.name
    properties: {
      startIpAddress: rule.startIpAddress
      endIpAddress: rule.endIpAddress
    }
  }
]

@description('Server name.')
output name string = server.name

@description('Server resource ID.')
output id string = server.id

@description('Host name clients connect to, e.g. psql-ee-dev-eus-001.postgres.database.azure.com.')
output fqdn string = server.properties.fullyQualifiedDomainName

@description('Application database name.')
output databaseName string = database.name

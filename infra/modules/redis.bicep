// P0-058.5 — the per-environment Azure Managed Redis cache and its single
// database (infra-conventions §3.1, §4, §7; ADR-0008).
//
//   amr-ee-<env>-<region>-<unique>     (DNS name — caller adds the suffix)
//
// Azure Managed Redis (Microsoft.Cache/redisEnterprise), not Azure Cache for
// Redis: the classic Basic/Standard/Premium tiers stopped accepting new
// instances on 2026-10-01 and retire on 2028-09-30 (ADR-0008).
//
// Fixed in the module:
//   - Access keys disabled (accessKeysAuthentication). Clients authenticate
//     with Entra ID; the API's managed identity gets a data-access policy
//     assignment in P0-062.3. No connection-string password exists to leak.
//   - TLS only (clientProtocol Encrypted), TLS 1.2 minimum.
//   - No persistence. This is a cache and a lock store — anything that must
//     survive a restart belongs in Postgres.
//   - Port 10000, the Azure Managed Redis default.
//   - dataClassification: confidential is always added to the tags — cached
//     values are tenant data.
//
// Create-time only, so chosen deliberately: clusteringPolicy. The default here
// is NoCluster — a single endpoint, multi-key commands and Lua scripts work
// unchanged, and it is the one policy that can later be switched to a
// clustered one in place. OSSCluster/EnterpriseCluster cannot be changed
// without deleting the database.

import { requiredTags } from '../types.bicep'

@description('Cache name, e.g. amr-ee-dev-eus-abcde (infra-conventions §3.1). Alphanumeric and single hyphens, 1–60 characters.')
@minLength(1)
@maxLength(60)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5). dataClassification: confidential is added by the module.')
param tags requiredTags

@description('Cache size (infra-conventions §7). Balanced_B0 is the smallest; larger tiers are added here when something needs them.')
@allowed([
  'Balanced_B0'
  'Balanced_B1'
  'Balanced_B3'
  'Balanced_B5'
])
param skuName string

@description('Replicate the data set to a second node. Off in dev/stg (no SLA, half the price), on in prod — locks must survive a node failure (infra-conventions §7).')
param highAvailability bool

@description('Clustering policy. Create-time only for the clustered values; NoCluster can be switched later, the others cannot.')
@allowed([
  'NoCluster'
  'OSSCluster'
  'EnterpriseCluster'
])
param clusteringPolicy string = 'NoCluster'

@description('What Redis evicts when memory is full. VolatileLRU only evicts keys with a TTL — every cache entry and lock is written with one.')
@allowed([
  'VolatileLRU'
  'VolatileLFU'
  'VolatileTTL'
  'AllKeysLRU'
  'AllKeysLFU'
  'NoEviction'
])
param evictionPolicy string = 'VolatileLRU'

resource cache 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: name
  location: location
  tags: union(tags, { dataClassification: 'confidential' })
  sku: {
    name: skuName
  }
  properties: {
    highAvailability: highAvailability ? 'Enabled' : 'Disabled'
    minimumTlsVersion: '1.2'
    // Public endpoint is the Phase 0 position (infra-conventions §9). With
    // access keys off, a caller still needs an Entra token and an access
    // policy assignment to do anything.
    publicNetworkAccess: 'Enabled'
  }
}

// Azure Managed Redis has exactly one database per cache, and it must be
// named 'default'.
resource database 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  parent: cache
  name: 'default'
  properties: {
    accessKeysAuthentication: 'Disabled'
    clientProtocol: 'Encrypted'
    clusteringPolicy: clusteringPolicy
    evictionPolicy: evictionPolicy
    port: 10000
  }
}

@description('Cache name.')
output name string = cache.name

@description('Cache resource ID.')
output id string = cache.id

@description('Database resource ID — the parent of the access-policy assignment in P0-062.3.')
output databaseId string = database.id

@description('Host name, e.g. amr-ee-dev-eus-abcde.eastus.redis.azure.net — what the Redis client connects to.')
output hostName string = cache.properties.hostName

@description('TLS port of the database endpoint.')
output port int = database.properties.port!

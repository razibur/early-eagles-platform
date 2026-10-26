// P0-058.4 — the per-environment Service Bus namespace, with its topics and
// subscriptions (infra-conventions §3.1, §4, §7).
//
//   sbns-ee-<env>-<region>-<unique>     (globally unique — caller adds the suffix)
//
// Generic on purpose: which topics and subscriptions exist is decided by the
// caller (P0-051 picks them against the outbox's EventType routing), not here.
//
// Fixed in the module:
//   - No Basic tier. Basic has queues but no topics, and the outbox publishes
//     to a topic (infra-conventions §7).
//   - SAS keys disabled (disableLocalAuth). Senders and receivers authenticate
//     with Entra ID; the API's managed identity is granted the data roles in
//     P0-062.3. No connection string exists to leak.
//   - Duplicate detection on every topic. The outbox drain is at-least-once —
//     it re-sends a row if it dies between publishing and stamping it — so
//     Service Bus drops a re-send whose MessageId it has already seen inside
//     the window. The publisher (P0-050.2.2) must set MessageId to the outbox
//     row's id. This can only be chosen when a topic is created.

import { requiredTags } from '../types.bicep'

@description('A subscription on a topic. Filters are added when a consumer needs one (P0-052.1).')
type subscription = {
  name: string
  @description('Deliveries before a message is dead-lettered. Default 10.')
  maxDeliveryCount: int?
}

@description('A topic and the subscriptions that read from it.')
type topic = {
  name: string
  subscriptions: subscription[]
}

@description('Namespace name, e.g. sbns-ee-dev-eus-abcde (infra-conventions §3.1). 6–50 characters, starts with a letter.')
@minLength(6)
@maxLength(50)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Namespace tier. Standard in every environment (infra-conventions §7); Basic is excluded because it has no topics.')
@allowed([
  'Standard'
  'Premium'
])
param skuName string

@description('Topics and their subscriptions. Chosen by the caller (P0-051).')
param topics topic[] = []

@description('How long a topic remembers MessageIds for duplicate detection (ISO 8601). Must cover the outbox drain\'s retry gap.')
param duplicateDetectionWindow string = 'PT10M'

@description('How long an undelivered message lives before it expires to the dead-letter queue (ISO 8601).')
param messageTimeToLive string = 'P14D'

@description('Principal (object) IDs that publish: Azure Service Bus Data Sender on the namespace (P0-062.3.2).')
param dataSenderPrincipalIds string[] = []

@description('Principal (object) IDs that consume: Azure Service Bus Data Receiver on the namespace (P0-062.3.2).')
param dataReceiverPrincipalIds string[] = []

// Built-in role IDs are the same in every tenant (learn.microsoft.com, Azure
// built-in roles → Integration).
var dataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var dataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

// One row per (principal, role) so both lists share one resource loop.
var dataRoleRows = concat(
  map(dataSenderPrincipalIds, p => { principalId: p, roleId: dataSenderRoleId }),
  map(dataReceiverPrincipalIds, p => { principalId: p, roleId: dataReceiverRoleId })
)

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    // Public endpoint is the Phase 0 position (infra-conventions §9).
    publicNetworkAccess: 'Enabled'
  }
}

resource topicResources 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = [
  for t in topics: {
    parent: namespace
    name: t.name
    properties: {
      requiresDuplicateDetection: true
      duplicateDetectionHistoryTimeWindow: duplicateDetectionWindow
      defaultMessageTimeToLive: messageTimeToLive
    }
  }
]

// Flatten topic → subscriptions so each subscription is one resource.
var subscriptionRows = flatten(map(
  range(0, length(topics)),
  i => map(topics[i].subscriptions, s => { topicIndex: i, sub: s })
))

resource subscriptionResources 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [
  for row in subscriptionRows: {
    parent: topicResources[row.topicIndex]
    name: row.sub.name
    properties: {
      maxDeliveryCount: row.sub.?maxDeliveryCount ?? 10
      deadLetteringOnMessageExpiration: true
      lockDuration: 'PT1M'
    }
  }
]

// Namespace scope, not per topic: topics come and go with P0-051, and a
// grant per entity would have to follow them. Data roles only. Neither one
// can manage entities or read keys (local auth is off anyway).
// principalType: see keyVault.bicep.
resource dataRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for row in dataRoleRows: {
    name: guid(namespace.id, row.principalId, row.roleId)
    scope: namespace
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', row.roleId)
      principalId: row.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

@description('Namespace name.')
output name string = namespace.name

@description('Namespace resource ID — the scope for the data-role assignments in P0-062.3.')
output id string = namespace.id

@description('Fully qualified namespace, e.g. sbns-ee-dev-eus-abcde.servicebus.windows.net — what the Service Bus client is configured with.')
output fullyQualifiedNamespace string = split(split(namespace.properties.serviceBusEndpoint, '//')[1], ':')[0]

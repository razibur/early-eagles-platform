// P0-059.4 — subscription-wide resources every environment uses
// (infra-conventions §1, §3.2). Today that is only the container registry.
//
// Deploy once per subscription, before any main.<env>.bicep that pulls from
// the registry (deployment name per §6):
//   az deployment sub create --name ee-shared-<yyyyMMdd>-<HHmm> --location eastus \
//     --template-file infra/main.shared.bicep --parameters infra/main.shared.bicepparam
//
// No sandbox switch: nothing here is purge-protected, and the registry is
// cheap enough to deploy for real the first time.

targetScope = 'subscription'

import { requiredTags } from 'types.bicep'

@description('owner tag (infra-conventions §5).')
param owner string

@description('costCenter tag (infra-conventions §5).')
param costCenter string

var environment = 'shared'
var location = 'eastus'
var regionCode = 'eus'

// §3.1 suffix, seeded with the subscription and 'shared'.
var unique = take(uniqueString(subscription().subscriptionId, environment), 5)

var resourceGroupName = 'rg-ee-${environment}-${regionCode}'

var tags requiredTags = {
  env: environment
  workload: 'early-eagles'
  owner: owner
  costCenter: costCenter
  managedBy: 'bicep'
}

var prefix = deployment().name

module rg 'modules/resourceGroup.bicep' = {
  name: '${prefix}-rg'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

module acr 'modules/containerRegistry.bicep' = {
  name: '${prefix}-acr'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [rg]
  params: {
    // No hyphens allowed (§3.2): cree + shared + eus + suffix = 18 chars.
    name: 'cree${environment}${regionCode}${unique}'
    location: location
    tags: tags
    skuName: 'Basic'
  }
}

@description('Resource group holding the shared resources.')
output resourceGroupName string = resourceGroupName

@description('Registry resource ID — the scope for each environment\'s AcrPull assignment (P0-062.4).')
output registryId string = acr.outputs.id

@description('Registry login server — the prefix of every image reference.')
output registryLoginServer string = acr.outputs.loginServer

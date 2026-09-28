// P0-057.1 — the per-environment resource group (infra-conventions §1).
//
// Resource groups live at subscription scope, so this module is deployed from a
// subscription-scoped main.<env>.bicep. It takes its name, region and tags as
// parameters and hard-codes none of them (infra-conventions rule 3); the caller
// builds `rg-ee-<env>-<region>`.

targetScope = 'subscription'

import { requiredTags } from '../types.bicep'

@description('Resource group name, e.g. rg-ee-dev-eus (infra-conventions §3).')
@minLength(1)
@maxLength(90)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: name
  location: location
  tags: tags
}

@description('Resource group name, for scoping child module deployments.')
output name string = rg.name

@description('Resource group resource ID.')
output id string = rg.id

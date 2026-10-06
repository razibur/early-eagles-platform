// P0-058.6 — the container registry, one per subscription, shared by every
// environment (infra-conventions §1, §3.2, §4, §7).
//
//   cree<env>eus<unique>   e.g. creesharedeusabcde   (no hyphens — caller adds the suffix)
//
// Deployed by main.shared.bicep into rg-ee-shared-eus, not by any
// main.<env>.bicep: CI builds an image once, pushes it here, and dev, staging
// and prod each run that same digest. Each environment's Container App pulls
// with its managed identity (AcrPull, P0-062.4).
//
// Fixed in the module:
//   - Admin user disabled. The admin user is a single shared username and
//     password with push rights; nothing should authenticate with it. Pushes
//     come from CI via Entra (OIDC federated credential), pulls from managed
//     identities.
//   - Anonymous pull disabled. Images are private.
//   - Role-assignment mode pinned to LegacyRegistryPermissions. In the newer
//     ABAC mode the built-in AcrPull/AcrPush roles no longer grant data access
//     (repository-scoped roles do), which would silently break P0-062.4.

import { requiredTags } from '../types.bicep'

@description('Registry name, e.g. creesharedeusabcde (infra-conventions §3.2). Globally unique, 5–50 alphanumerics, no hyphens.')
@minLength(5)
@maxLength(50)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5). env is \'shared\'.')
param tags requiredTags

@description('Registry tier (infra-conventions §7). Basic is enough until geo-replication, private endpoints or retention policies are needed — all Premium-only.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param skuName string

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    roleAssignmentMode: 'LegacyRegistryPermissions'
    // Public endpoint is the Phase 0 position (infra-conventions §9). Every
    // pull and push still needs an Entra identity with a registry role.
    publicNetworkAccess: 'Enabled'
  }
}

@description('Registry name.')
output name string = registry.name

@description('Registry resource ID — the scope for the AcrPull assignments in P0-062.4.')
output id string = registry.id

@description('Login server, e.g. creesharedeusabcde.azurecr.io — the prefix of every image reference.')
output loginServer string = registry.properties.loginServer

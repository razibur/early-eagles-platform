// P0-057.2 — the per-environment Key Vault (infra-conventions §4, §7).
//
// Name, region, tags and SKU come from the caller (infra-conventions rule 3).
// Key Vault names are globally unique, so the caller appends the
// uniqueString suffix from infra-conventions §3.1:
//   kv-ee-<env>-<region>-<unique>
//
// Deliberately NOT parameters — these hold in every environment:
//   - RBAC authorization, not access policies. Access is granted with role
//     assignments to managed identities (P0-062.3), the same model as every
//     other resource, instead of a vault-local permission list.
//   - Soft-delete and purge protection on (infra-conventions §7). Purge
//     protection cannot be turned off once enabled, and a deleted vault's name
//     stays reserved for the retention window — so a dev vault torn down and
//     recreated under the same name must be recovered, not redeployed.

import { requiredTags } from '../types.bicep'

@description('Key Vault name, e.g. kv-ee-dev-eus-abcde (infra-conventions §3.1). Globally unique; must start with a letter.')
@minLength(3)
@maxLength(24)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Vault SKU. Standard in every environment (infra-conventions §7); premium only adds HSM-backed keys.')
@allowed([
  'standard'
  'premium'
])
param skuName string

@description('Days a deleted vault or secret stays recoverable. Cannot be changed after the vault is created.')
@minValue(7)
@maxValue(90)
param softDeleteRetentionInDays int = 90

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: skuName
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: true
    // Public endpoint is the Phase 0 position (infra-conventions §9); private
    // endpoints are a Phase 7 decision.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

@description('Key Vault name.')
output name string = vault.name

@description('Key Vault resource ID, for role-assignment scopes (P0-062.3).')
output id string = vault.id

@description('Vault URI, e.g. https://kv-ee-dev-eus-abcde.vault.azure.net/ — what the API is configured with.')
output uri string = vault.properties.vaultUri

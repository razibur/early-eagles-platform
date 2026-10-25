// P0-062.1 — a user-assigned managed identity (infra-conventions §3, §4).
//
//   id-ee-<role>-<env>-<region>     e.g. id-ee-api-dev-eus
//
// User-assigned rather than system-assigned, because the identity has to
// exist — and hold its role assignments — before the Container App does:
//   - P0-062.4 pulls the private ghcr.io image with a token read from Key
//     Vault *by this identity*. A system-assigned identity is only created
//     with the app, so the app's first revision could never pull its image.
//   - P0-062.2/.3 grant Postgres, Key Vault and Service Bus access to the
//     principal ID; with a user-assigned identity those grants survive the
//     app being deleted and recreated.
//
// One identity per role (api, later worker / migrations), not one shared by
// everything, so each gets only the access it needs.

import { requiredTags } from '../types.bicep'

@description('Identity name, e.g. id-ee-api-dev-eus (infra-conventions §3).')
@minLength(3)
@maxLength(128)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: tags
}

@description('Identity name.')
output name string = identity.name

@description('Identity resource ID — what a Container App lists under identity.userAssignedIdentities.')
output id string = identity.id

@description('Service principal object ID — the principal for role assignments (P0-062.2, P0-062.3).')
output principalId string = identity.properties.principalId

@description('Application (client) ID — what the app sets as AZURE_CLIENT_ID so DefaultAzureCredential picks this identity.')
output clientId string = identity.properties.clientId

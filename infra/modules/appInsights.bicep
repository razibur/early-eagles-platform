// P0-057.4 — the per-environment Application Insights component (infra-conventions §4).
//
// One per environment, a singleton, so no instance suffix:
//   appi-ee-<env>-<region>
//
// Workspace-based: telemetry is stored in the Log Analytics workspace from
// P0-057.3, so retention and the daily ingestion cap are that workspace's —
// there is no second cap or retention setting here to drift out of sync.

import { requiredTags } from '../types.bicep'

@description('Component name, e.g. appi-ee-dev-eus (infra-conventions §3).')
@minLength(1)
@maxLength(260)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Resource ID of the Log Analytics workspace that stores the telemetry (logAnalytics.bicep output `id`).')
param workspaceId string

@description('When true, ingestion requires Entra ID auth and the connection string alone is not enough. Left off until the OpenTelemetry exporter is wired (P0-089.1) and can authenticate with the managed identity.')
param disableLocalAuth bool = false

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: disableLocalAuth
    // Public endpoints are the Phase 0 position (infra-conventions §9).
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Component name.')
output name string = component.name

@description('Component resource ID.')
output id string = component.id

// Not a credential in the Key Vault sense — it only permits sending telemetry —
// but it is marked secure so it stays out of deployment history and logs.
@description('Connection string the API and web app use to send telemetry.')
@secure()
output connectionString string = component.properties.ConnectionString

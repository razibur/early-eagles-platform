// P0-058.2 — the per-environment Container Apps environment (infra-conventions §4, §7).
//
// One per environment, a singleton, so no instance suffix:
//   cae-ee-<env>-<region>
// The API, worker and migrations Container Apps (P0-058.3) run inside it.
//
// Logs go to the Log Analytics workspace from P0-057.3 through the
// `azure-monitor` destination plus a diagnostic setting — not the
// `log-analytics` destination, which needs the workspace's shared key copied
// into this resource. No key means nothing to rotate or leak, and Azure
// Monitor is also the only route that keeps working behind Private Link
// (infra-conventions §9, Phase 7). Either way the workspace's daily cap applies.
//
// Deliberately absent:
//   - Dapr. The plan listed it; nothing in the stack (ADR-0002) uses it, and
//     Service Bus is reached directly from the API.
//   - VNet integration and zone redundancy. Zone redundancy requires a VNet;
//     both are Phase 7 decisions (infra-conventions §9).

import { requiredTags } from '../types.bicep'

@description('Environment name, e.g. cae-ee-dev-eus (infra-conventions §3).')
@minLength(2)
@maxLength(60)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Resource ID of the Log Analytics workspace that receives the logs (logAnalytics.bicep output `id`).')
param workspaceId string

resource environment 'Microsoft.App/managedEnvironments@2026-01-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    // Serverless Consumption profile: scale-to-zero in dev, pay per use
    // (infra-conventions §7). Dedicated profiles can be added later without
    // recreating the environment.
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    // Public ingress is the Phase 0 position (infra-conventions §9).
    publicNetworkAccess: 'Enabled'
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  name: 'to-${last(split(workspaceId, '/'))}'
  properties: {
    workspaceId: workspaceId
    // Console (stdout/stderr) + system logs. Metrics are left out: they are
    // already in Azure Monitor metrics, and copying them into the workspace
    // would only spend the daily cap.
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
  }
}

@description('Environment name.')
output name string = environment.name

@description('Environment resource ID — what each Container App (P0-058.3) sets as its managedEnvironmentId.')
output id string = environment.id

@description('Default domain, e.g. <hash>.eastus.azurecontainerapps.io. App FQDNs are <app>.<defaultDomain>.')
output defaultDomain string = environment.properties.defaultDomain

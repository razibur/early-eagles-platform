// P0-057.3 — the per-environment Log Analytics workspace (infra-conventions §4, §7).
//
// One workspace per environment, a singleton, so no instance suffix:
//   log-ee-<env>-<region>
// App Insights (P0-057.4) and the Container Apps environment (P0-058.2) send
// their telemetry here.
//
// The daily cap is a required parameter with no "unlimited" escape hatch.
// Ingestion is the classic runaway bill — a logging loop can outspend the
// database in a day — so every environment must choose a number
// (infra-conventions §7).

import { requiredTags } from '../types.bicep'

@description('Workspace name, e.g. log-ee-dev-eus (infra-conventions §3). Alphanumerics and hyphens; must start and end alphanumeric.')
@minLength(4)
@maxLength(63)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Pricing tier. Pay-as-you-go in every environment (infra-conventions §7); commitment tiers need extra properties this module does not set.')
@allowed([
  'PerGB2018'
])
param skuName string

@description('Days of interactive retention. 30 is included in the ingestion price; more is billed per GB-month.')
@minValue(30)
@maxValue(730)
param retentionInDays int

@description('Daily ingestion cap in GB. Required — there is no unlimited option (infra-conventions §7). When hit, ingestion stops until the daily reset.')
@minValue(1)
param dailyQuotaGb int

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: skuName
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
    features: {
      // Access follows the resource that sent the log, so granting someone
      // read on one Container App does not expose every table in the workspace.
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    // Public endpoints are the Phase 0 position (infra-conventions §9).
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Workspace name.')
output name string = workspace.name

@description('Workspace resource ID — what App Insights (P0-057.4) and the Container Apps environment (P0-058.2) link to.')
output id string = workspace.id

@description('Workspace (customer) ID, the GUID agents and KQL tooling use to address it.')
output customerId string = workspace.properties.customerId

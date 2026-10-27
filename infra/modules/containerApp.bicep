// P0-058.3 — one Container App inside the environment from P0-058.2
// (infra-conventions §3, §4, §7).
//
//   ca-ee-<role>-<env>-<region>-<instance>     e.g. ca-ee-api-dev-eus-001
//
// Container Apps cap names at 32 characters — the tightest limit in the stack
// (infra-conventions §3.3) — so keep roles short (api, worker, migrations).
//
// Identity (P0-062.1): pass a user-assigned identity and it is attached to
// the app, with its client ID exported as AZURE_CLIENT_ID so
// DefaultAzureCredential uses it. No system-assigned identity: see
// userAssignedIdentity.bicep for why.
//
// Private registry (P0-062.4): pass `registry` and the app pulls with a
// password that is a Key Vault secret, read through the identity (which
// therefore needs Key Vault Secrets User — P0-062.3.1). For ghcr.io, which
// accepts no managed identity, the password is a read:packages token
// (ADR-0010). No registry = the image must be publicly pullable.
//
// Not here yet, by design:
//   - Health probes are opt-in (livenessPath / readinessPath). A placeholder
//     image doesn't serve /health or /ready, and a probe it can't pass leaves
//     the revision unhealthy forever.

import { requiredTags } from '../types.bicep'

@description('A plain (non-secret) environment variable. Secrets come from Key Vault references once managed identity lands (P0-062).')
type envVar = {
  name: string
  value: string
}

@description('A user-assigned identity. Typed as a pair so the client ID cannot be forgotten.')
type identityRef = {
  @description('Resource ID.')
  id: string

  @description('Client (application) ID, set as AZURE_CLIENT_ID.')
  clientId: string
}

@description('A private registry whose password lives in Key Vault.')
type registryRef = {
  @description('Registry host, e.g. ghcr.io.')
  server: string

  @description('User name the registry expects with the password. For ghcr.io: the token owner\'s GitHub login.')
  username: string

  @description('Key Vault secret URL without a version, e.g. https://kv-…vault.azure.net/secrets/ghcr-pull-token. Version-less so a rotated value is picked up.')
  passwordKeyVaultSecretUrl: string
}

@description('App name, e.g. ca-ee-api-dev-eus-001 (infra-conventions §3). Lowercase alphanumerics and hyphens, at most 32 characters.')
@minLength(2)
@maxLength(32)
param name string

@description('Azure region, e.g. eastus (infra-conventions §2).')
param location string

@description('Required tags (infra-conventions §5).')
param tags requiredTags

@description('Resource ID of the Container Apps environment (containerAppsEnv.bicep output `id`).')
param environmentId string

@description('Container image, e.g. mcr.microsoft.com/k8se/quickstart:latest until the registry exists.')
param image string

@description('Port the container listens on.')
@minValue(1)
@maxValue(65535)
param targetPort int

@description('True to accept traffic from the internet; false for environment-internal only.')
param externalIngress bool

@description('vCPU per replica. Memory is set to the Consumption profile\'s fixed 2 GiB-per-vCPU ratio.')
@allowed([
  '0.25'
  '0.5'
  '0.75'
  '1'
  '1.25'
  '1.5'
  '1.75'
  '2'
])
param cpu string

@description('Minimum replicas. 0 in dev/stg (scale-to-zero); 1 in prod so the first request of the day is not a cold start (infra-conventions §7).')
@minValue(0)
@maxValue(10)
param minReplicas int

@description('Maximum replicas.')
@minValue(1)
@maxValue(10)
param maxReplicas int

@description('Plain environment variables.')
param env envVar[] = []

@description('User-assigned identity to attach: userAssignedIdentity.bicep outputs `id` and `clientId`. Null = no identity.')
param identity identityRef?

@description('Private registry to pull `image` from. Requires `identity`: the password is read from Key Vault as that identity. Null = public image.')
param registry registryRef?

@description('HTTP path for the liveness probe, e.g. /health. Empty = no probe (placeholder images).')
param livenessPath string = ''

@description('HTTP path for the readiness probe, e.g. /ready. Empty = no probe (placeholder images).')
param readinessPath string = ''

var memoryByCpu = {
  '0.25': '0.5Gi'
  '0.5': '1Gi'
  '0.75': '1.5Gi'
  '1': '2Gi'
  '1.25': '2.5Gi'
  '1.5': '3Gi'
  '1.75': '3.5Gi'
  '2': '4Gi'
}

var livenessProbe = empty(livenessPath)
  ? []
  : [
      {
        type: 'Liveness'
        httpGet: {
          path: livenessPath
          port: targetPort
        }
        periodSeconds: 30
        failureThreshold: 3
      }
    ]

var readinessProbe = empty(readinessPath)
  ? []
  : [
      {
        type: 'Readiness'
        httpGet: {
          path: readinessPath
          port: targetPort
        }
        periodSeconds: 10
        failureThreshold: 3
      }
    ]

// With more than one identity attached, DefaultAzureCredential can't guess
// which to use; naming it explicitly also keeps a later second identity from
// silently changing what the app authenticates as.
var identityEnv = identity == null
  ? []
  : [
      {
        name: 'AZURE_CLIENT_ID'
        value: identity!.clientId
      }
    ]

// The registry password is an app secret that is a Key Vault reference, so
// the token's value never passes through the deployment.
var registrySecretName = 'registry-password'

var registrySecrets = registry == null
  ? []
  : [
      {
        name: registrySecretName
        keyVaultUrl: registry!.passwordKeyVaultSecretUrl
        // Fails validation with this message rather than a null dereference.
        identity: (identity ?? fail('containerApp.bicep: registry requires identity (it reads the password from Key Vault).')).id
      }
    ]

var registries = registry == null
  ? []
  : [
      {
        server: registry!.server
        username: registry!.username
        passwordSecretRef: registrySecretName
      }
    ]

resource app 'Microsoft.App/containerApps@2026-01-01' = {
  name: name
  location: location
  tags: tags
  identity: identity == null
    ? { type: 'None' }
    : {
        type: 'UserAssigned'
        userAssignedIdentities: {
          '${identity!.id}': {}
        }
      }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      // One revision takes all traffic; a new deploy replaces it. Blue/green
      // via multiple revisions is a later CI/CD concern (P0-084+).
      activeRevisionsMode: 'Single'
      secrets: registrySecrets
      registries: registries
      ingress: {
        external: externalIngress
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'app'
          image: image
          resources: {
            cpu: json(cpu)
            memory: memoryByCpu[cpu]
          }
          env: concat(identityEnv, env)
          probes: concat(livenessProbe, readinessProbe)
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

@description('App name.')
output name string = app.name

@description('App resource ID.')
output id string = app.id

@description('Ingress host name, e.g. ca-ee-api-dev-eus-001.<hash>.eastus.azurecontainerapps.io.')
output fqdn string = app.properties.configuration.ingress.fqdn

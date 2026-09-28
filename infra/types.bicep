// Shared user-defined types for every module under infra/.
// See docs/infra-conventions.md — this file is the compile-time half of §5.

@description('Environment code. Matches the <env> segment of every resource name (infra-conventions §1).')
@export()
type environmentCode = 'dev' | 'stg' | 'prod'

// The five tags every resource must carry (infra-conventions §5). Typing the
// `tags` parameter with this makes a missing required tag a build error rather
// than a review comment. Extra string-valued tags (dataClassification,
// criticality) are allowed through the `*` index signature.
@description('Required resource tags. Azure does not inherit tags from a resource group, so every module applies these itself.')
@export()
type requiredTags = {
  env: environmentCode
  workload: 'early-eagles'
  owner: string
  costCenter: string
  managedBy: 'bicep'
  *: string
}

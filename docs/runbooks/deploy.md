# Runbook — shipping a change (P0-097.1)

The whole path from a merged PR to a verified environment. The detailed commands live in the
runbooks this one links; this page is the order to do them in.

| Environment | Where it runs | How it gets a new version |
|---|---|---|
| **dev** | your machine ([ADR-0009](../adr/0009-local-dev-no-cloud-dev-environment.md)) | `git pull`, then the README quick start |
| **sandbox** | throwaway Azure resource group | by hand, to try infrastructure changes ([`deploy-infra.md`](./deploy-infra.md) §11) |
| **staging** | Azure, on demand, torn down after use | by hand today; tag-triggered workflow later (P0-086) |
| **prod** | Azure | by hand today; manual-approval workflow later (P0-087) |

## 1. Merge to `main`

A PR merges only with green `backend` (build, unit tests, tenant-isolation gate) and `web` (lint,
build, browser tests) checks. On `main`, `api-image.yml` then builds the API image and pushes
`ghcr.io/razibur/early-eagles-api:sha-<commit>`. Its run summary prints the exact reference. No image
exists for a commit whose tests failed.

## 2. Schema first, if the release changes it

Apply the migrations **before** the app that needs them, following [`db-migrate.md`](./db-migrate.md)
§2: generate the idempotent script, read it, note the restore time, and apply it as the Entra admin.
Expand-only changes are safe for the version that's still running.

## 3. Deploy the image

```pwsh
$env:EE_API_IMAGE = 'ghcr.io/razibur/early-eagles-api:sha-<commit>'
$env:EE_API_PORT  = '8080'
```

Then [`deploy-infra.md`](./deploy-infra.md) §4 (what-if: expect only the Container App to change) and
§5 (deploy). Always a `sha-` tag, never the moving `main` tag, so what runs is what was tested.

## 4. Verify

| Check | Expect |
|---|---|
| `GET https://<app>/health` | 200, `version` = `1.0.0+<commit>` you deployed |
| `GET https://<app>/ready` | 200, `postgres` (and `servicebus` where deployed) Healthy |
| `az containerapp revision list -n ca-ee-api-<env>-eus-001 -g rg-ee-<env>-eus -o table` | one active revision, the new image |
| App Insights → Failures, last 15 min | no new exception types |

## 5. If it goes wrong

Roll the app back first ([`rollback.md`](./rollback.md)), then investigate
([`incident-response.md`](./incident-response.md)).

## What's not automated yet

Staging and prod deploys (P0-086/087) and an automated migration runner (P0-088) need the Azure
subscription and a GitHub→Azure identity (OIDC). Until then every Azure step above is a person
running the linked commands.

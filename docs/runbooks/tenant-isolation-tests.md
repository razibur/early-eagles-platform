# Runbook — Tenant Isolation Tests

> **The gate.** Tenant isolation is the load-bearing invariant of this platform:
> a request scoped to Tenant A must never read or write Tenant B's rows. The
> tests documented here are the CI gate that proves it. Every one of them must
> stay green before any feature beyond Phase 0 ships. If you're touching
> persistence, tenancy, or adding a new tenant-scoped entity, read this first.

Task IDs: **P0-034.\*** in [PROGRESS.md](../../PROGRESS.md) and
[Implementation-Plan.md](../../Implementation-Plan.md).

---

## 1. What isolation is made of

Isolation is enforced in three places, and every test in this runbook exercises
at least one of them.

| Layer | Where | Responsibility |
|---|---|---|
| **Marker** | [`ITenantScoped`](../../apps/api/src/EarlyEagles.Domain/Primitives/ITenantScoped.cs) | The single source of truth. Any entity that carries tenant data implements this and exposes `Guid TenantId`. |
| **Read-side filter** | [`ApplicationDbContext.OnModelCreating`](../../apps/api/src/EarlyEagles.Infrastructure/Persistence/ApplicationDbContext.cs) | EF Core global query filter — `EF.Property<Guid>(e, col) == CurrentTenantId` — applied to every `ITenantScoped` type at model-build time. |
| **Write-side interceptor** | [`TenantStampingInterceptor`](../../apps/api/src/EarlyEagles.Infrastructure/Persistence/Interceptors/TenantStampingInterceptor.cs) | `SaveChangesInterceptor` that walks the change tracker on every save, stamps missing tenant ids on inserts, and rejects any write that targets a different tenant. |

`CurrentTenantId` is snapshotted from `ITenantContext` when the DbContext is
constructed. When no tenant is resolved it snapshots `Guid.Empty` — matching no
row, so reads fail closed. The interceptor mirrors that: writes without a
resolved tenant AND without an explicit `TenantId` are rejected outright.

The self-scoped `Tenant` aggregate is a special case both layers know about:
its tenant id *is* its primary key, so the filter compares against `Id` and the
interceptor exempts inserts (provisioning a tenant predates its own existence).

---

## 2. Prerequisites

- **Docker.** The suite spins up an ephemeral `postgres:17` container via
  [Testcontainers](https://dotnet.testcontainers.org/). Docker Desktop (or any
  Docker daemon on the `DOCKER_HOST` your OS defaults to) must be running. CI
  needs the same.
- **.NET 10 SDK.** Whatever `global.json` pins.
- Nothing else — no local Postgres, no seeded fixtures on disk, no manual setup.

---

## 3. Where the tests live

All under
[`apps/api/tests/EarlyEagles.Tests.Integration/Persistence/`](../../apps/api/tests/EarlyEagles.Tests.Integration/Persistence/).

| File | Task | What it proves |
|---|---|---|
| `PostgresFixture.cs` | P0-034.1, .2 | The fixture itself. Starts the container, applies migrations, seeds Alpha + Beta with members, exposes `CreateContext(...)`. |
| `PostgresCollectionDefinition.cs` | P0-034.1 | Binds the fixture to an xUnit collection so it's shared across the isolation tests. |
| `PostgresFixtureSmokeTests.cs` | P0-034.1 | Sanity: container up, schema applied. First to fail if Docker isn't running. |
| `TenantQueryFilterTests.cs` | P0-032.2 | Asserts the SQL emitted by the filter via `ToQueryString()`. No DB round-trip needed for the shape check. |
| `TenantIsolationTests.cs` | **P0-034.3** | **The headline gate.** Reading as Alpha never surfaces Beta's rows, even by primary-key lookup. |
| `TenantWriteStampingTests.cs` | **P0-034.4** | The write-side twin: the interceptor stamps missing tenant ids and rejects cross-tenant writes. |
| `UnresolvedTenantContextTests.cs` | **P0-034.5** | Default deny: a context with no resolved tenant sees zero rows. Covers the "middleware misfires or is bypassed" path. |
| `CrossTenantEndpointTests.cs` | **P0-034.6** | HTTP-layer proof: an Alpha-scoped request that addresses a Beta member by id gets 404 from a naive `FirstOrDefaultAsync` — the filter, not a hand-rolled check, is what enforces isolation. |

---

## 4. How to run

From `apps/api/`:

```pwsh
# Just the isolation gate
dotnet test tests/EarlyEagles.Tests.Integration `
  --filter "FullyQualifiedName~Persistence"

# Or the full integration suite (isolation + everything else)
dotnet test tests/EarlyEagles.Tests.Integration
```

First run pulls the `postgres:17` image (~150 MB). Subsequent runs reuse the
cached image and take ~5s total for the whole integration suite because
Testcontainers boots one container per test collection.

If the tests hang on startup, Docker isn't reachable. Check `docker ps`.

---

## 5. The test pattern

Every isolation test follows the same shape. If you're adding one, copy this:

```csharp
[Collection(PostgresCollectionDefinition.Name)]
public class MyNewIsolationTests
{
    private readonly PostgresFixture fixture;

    public MyNewIsolationTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task My_Assertion()
    {
        // "Operate as Alpha" — the ambient tenant is resolved, the filter
        // scopes every read, the interceptor validates every write.
        await using var context = fixture.CreateContext(
            PostgresFixture.AlphaTenantId,
            PostgresFixture.AlphaSlug);

        // ... your assertions ...
    }
}
```

Three flavours of context available from the fixture — pick the one that
matches what you're testing:

| Call | Ambient tenant | Use when |
|---|---|---|
| `CreateContext(AlphaTenantId, AlphaSlug)` | resolved to Alpha | Simulating a real request scoped to a tenant. |
| `CreateContext(BetaTenantId, BetaSlug)` | resolved to Beta | Ditto, from the other side of the fence. |
| `CreateContext(new TenantContext())` | **unresolved** — `IsResolved = false` | Simulating middleware failure, background jobs, or explicit-tenant writes. Reads return empty; writes must set `TenantId` explicitly or be rejected. |

Seed constants live on the fixture:
`AlphaTenantId`, `AlphaSlug`, `AlphaAdminUserId`, `AlphaMemberUserId`,
`BetaTenantId`, `BetaSlug`, `BetaAdminUserId`.

---

## 6. Adding a new tenant-scoped entity without breaking the gate

The unit-test suite has a **reflection guard**
([`TenantScopedMarkerTests.cs`](../../apps/api/tests/EarlyEagles.Tests.Unit/Tenancy/TenantScopedMarkerTests.cs))
that asserts *every* concrete `Entity<>` in the Domain assembly implements
`ITenantScoped`. If you add a domain entity and forget the marker, the unit
tests fail before the integration tests even start — that's deliberate.

The workflow for a new tenant-scoped entity:

1. **Implement `ITenantScoped`** on the entity. Expose `TenantId`. For
   aggregates the field is real; for entities whose identity is
   `(TenantId, Something)` a projection like `TenantId => Id.TenantId` is fine.
2. **Add a `DbSet<T>`** on `ApplicationDbContext`. The query filter is applied
   automatically in `OnModelCreating` because it loops over
   `ITenantScoped` types — no per-entity plumbing.
3. **Add an EF Core configuration** under
   `apps/api/src/EarlyEagles.Infrastructure/Persistence/Configurations/`.
   Make sure the column that holds `TenantId` is called exactly `TenantId`
   (the filter's discriminator resolver keys off the property name).
4. **Add a migration** — `dotnet ef migrations add <name>` from
   `apps/api/src/EarlyEagles.Infrastructure/`.
5. **Add a test to `TenantIsolationTests.cs`** (or a peer file) proving:
   as Alpha you see only Alpha's rows; as Beta only Beta's; as unresolved,
   none.

The write-side interceptor needs no changes — it walks
`ITenantScoped` entries generically.

---

## 7. Opting out (and when it is legitimate)

Both halves of isolation can be bypassed. The escape hatches exist so
platform-level work (migrations, seeding, provisioning, background jobs)
actually functions — the bar is that every use of them is *deliberate*.

**Reads** — `IgnoreQueryFilters()`:

```csharp
var allTenants = await context.Tenants.IgnoreQueryFilters().ToListAsync();
```

Used by the pre-auth tenant-resolution lookup (which happens *before* a tenant
is known) and by the "backstop" assertion in each isolation test file that
proves the seeded rows really are in the DB.

**Writes** — supply an explicit `TenantId` when no tenant is resolved:

```csharp
using var context = fixture.CreateContext(new TenantContext());
context.TenantMembers.Add(TenantMember.Create(AlphaTenantId, ...));
await context.SaveChangesAsync(); // Allowed: no ambient, but TenantId is explicit.
```

Used by `PostgresFixture.SeedAsync` and by any legitimate cross-tenant
provisioning. Rejected: an insert with **neither** an ambient tenant **nor** an
explicit `TenantId` — the interceptor throws `CrossTenantWriteException`
because there is nothing to stamp from and the resulting row would belong to
nobody.

If you find yourself opting out inside application/handler code (not
infrastructure), that's a review flag: it means the request is doing something
tenant-scoped code should be doing.

---

## 8. Known gap — `ExecuteUpdate` / `ExecuteDelete`

EF Core's `ExecuteUpdate` and `ExecuteDelete` bypass the change tracker, so
`SaveChanges` — and therefore `TenantStampingInterceptor` — never runs. The
read-side global query filter still applies, so bulk operations only touch
the ambient tenant unless the caller adds `IgnoreQueryFilters()`. In practice
that means:

- `ctx.TenantMembers.Where(...).ExecuteDelete()` — safe: filtered to ambient.
- `ctx.TenantMembers.IgnoreQueryFilters().Where(...).ExecuteDelete()` —
  **dangerous**: no interceptor to catch cross-tenant scope. Only use in
  migrations / provisioning, and add a test if you do.

This gap is documented on the interceptor and accepted for Phase 0. If a
tenant-scoped feature grows a need for bulk cross-tenant writes, revisit here
and add an explicit guard.

---

## 9. What each test proves, in one line

Use this as a checklist when reviewing a change to persistence or tenancy.

**Read side — `TenantIsolationTests`:**
- Alpha context sees only Alpha members.
- Beta context sees only Beta members.
- Alpha context, primary-key lookup for a Beta member → `null`.
- `Tenant` aggregate is self-scoped: Alpha sees only its own row.
- `IgnoreQueryFilters()` reveals both — filter is what hides them, not
  missing data.

**Read side — `UnresolvedTenantContextTests`:**
- Unresolved context → `TenantMembers` empty.
- Unresolved context → `Tenants` empty (self-scoped still keys off `Guid.Empty`).
- Unresolved context, PK lookup → `null`.
- `IgnoreQueryFilters()` reveals seeded rows — backstop again.

**HTTP surface — `CrossTenantEndpointTests`:**
- `alpha.earlyeagles.club` requesting own admin → 200 with the member body.
- `alpha.earlyeagles.club` requesting a Beta member's id → **404**.
- `beta.earlyeagles.club` requesting an Alpha member's id → **404**.
- `beta.earlyeagles.club` requesting own admin → 200.
- Unknown id and cross-tenant id return identical 404s — the caller cannot
  distinguish "no such user" from "belongs to another tenant".

**Write side — `TenantWriteStampingTests`:**
- Insert with no `TenantId` + resolved tenant → stamped with ambient.
- Insert with no `TenantId` + no resolved tenant → rejected.
- Insert with a different tenant's id → rejected.
- Update that would move a row between tenants → rejected.
- Delete of another tenant's row → rejected.
- Insert with explicit `TenantId` + no resolved tenant → allowed (platform work).
- Tenant aggregate: insert with `Id == Guid.Empty` context → allowed
  (provisioning is exempt from the ambient check).
- Update / delete of a different tenant's `Tenant` row → rejected.
- Interceptor is registered from `OnConfiguring`, so however the context was
  built the guard applies.

---

## 10. If a test fails

1. **Docker not running.** `PostgresFixtureSmokeTests` fails first. Start
   Docker Desktop.
2. **A new `Entity<>` doesn't implement `ITenantScoped`.** The unit-test
   reflection guard (`TenantScopedMarkerTests`) fires with the offending type
   name. Add the marker.
3. **Missing `TenantId` column on a new entity's mapping.** The filter's
   discriminator resolver falls back to the primary key, which is wrong for
   anything other than the self-scoped `Tenant`. Name the column exactly
   `TenantId` in the EF configuration.
4. **A handler forgot to set `TenantId` and no tenant is resolved.** The
   interceptor throws `CrossTenantWriteException` with the entity type and
   state. Resolve a tenant upstream, or if it's platform code, set `TenantId`
   explicitly.
5. **The gate itself failed** — Alpha suddenly sees Beta's rows. Do not merge.
   Bisect back to the change that broke it. This is the load-bearing invariant.

---

## 11. Wiring into CI

The suite runs in [`.github/workflows/backend.yml`](../../.github/workflows/backend.yml)
as its own job: **`integration tests (tenant-isolation gate)`**. It sits
alongside a `build + unit tests` job that catches compile / analyzer /
warnings-as-error regressions in the fast lane.

Both jobs run on every PR to `main` and every push to `main`. GitHub-hosted
`ubuntu-latest` runners include Docker with the daemon running, so
Testcontainers spins up `postgres:17` without extra service definitions. The
suite is self-contained — no secrets, no external DB, no cache to warm.

**Required check (branch protection).** The workflow file gets you the failing
status; making it *block merges* is a one-time repo-settings action:

1. Repo → Settings → Branches → add a branch-protection rule for `main`.
2. Enable **Require status checks to pass before merging**.
3. Add `integration tests (tenant-isolation gate)` to the required set. Add
   `build + unit tests` too — a merge with a broken build is never right.
4. Enable **Require branches to be up to date before merging** so the check
   ran against the actual merged state, not a stale ancestor.

There is no path filter on the workflow, so a required check always reports
on every PR (see the comment in the workflow file for the rationale).

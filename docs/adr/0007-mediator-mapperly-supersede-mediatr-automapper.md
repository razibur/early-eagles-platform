# ADR-0007: martinothamar/Mediator + Mapperly supersede MediatR + AutoMapper

- **Status:** Accepted (supersedes the MediatR + AutoMapper picks in [ADR-0002](./0002-enterprise-saas-reframe.md), and the matching task descriptions in [`Implementation-Plan.md`](../../Implementation-Plan.md) P0-023.1 / P0-023.3 + P0-024.* pipeline behaviors)
- **Date:** 2026-06-18
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`CLAUDE.md`](../../CLAUDE.md), [`Plan.md`](../../Plan.md), [`Implementation-Plan.md`](../../Implementation-Plan.md), [`PROGRESS.md`](../../PROGRESS.md)

## Context

ADR-0002 picked **MediatR** (in-process mediator + pipeline behaviors) and **AutoMapper** (object-to-object mapping) for the Application layer. Both libraries were authored by Jimmy Bogard and historically Apache-2.0–licensed.

Both projects moved to **commercial licensing** under Lucky Penny Software in late 2024 / early 2025. The current NuGet packages are published under `LuckyPennySoftware.MediatR` and `LuckyPennySoftware.AutoMapper`; the runtime emits a license-key warning when used in a commercial product without a paid key. EarlyEagles is an enterprise B2B SaaS we intend to sell — i.e. exactly the use case the new licenses are designed to monetize.

Paid licenses are real-money / per-developer / annual. Solo-builder phase makes the bill modest; bringing in any contractor or open-source contributor compounds it. The plan is to keep operating costs minimal until tenants are paying (see [`Plan.md`](../../Plan.md) cost model), so an unbounded per-developer license line item is a poor fit.

Free, mature, .NET-native alternatives exist for both responsibilities:

- **Mediator pattern** — `martinothamar/Mediator` provides a near-drop-in MediatR-compatible API (`IRequest<>`, `IRequestHandler<,>`, pipeline behaviors, notifications) implemented with C# source generators. MIT-licensed. AOT-compatible. Measurably faster than MediatR at runtime because there is no reflection on the hot path. ASP.NET Core DI registration via `services.AddMediator(...)` is identical in shape to MediatR's `services.AddMediatR(...)`.
- **Object mapping** — `Riok.Mapperly` is a compile-time source generator. Mapping methods are declared as partial methods on a mapper class decorated with `[Mapper]`; the generator emits the implementation. No runtime configuration, no profiles, no `IMapper` service to register — the generated mapper classes are concrete types injected directly. Trimming-safe, AOT-safe, zero reflection.
- **Validation** — `FluentValidation` itself remains MIT-licensed (Jeremy Skinner is the maintainer, not part of the Lucky Penny portfolio). Keep as planned.

## Decision

Application-layer Phase 0 packages:

| Concern | Old pick (ADR-0002) | New pick (this ADR) | License |
|---|---|---|---|
| Mediator | MediatR | **martinothamar/Mediator** (`Mediator.SourceGenerator` + `Mediator.Abstractions`) | MIT |
| Validation | FluentValidation | **FluentValidation** *(unchanged)* | MIT |
| Mapping | AutoMapper | **Riok.Mapperly** | MIT |

Concretely:

- `EarlyEagles.Application.csproj` references `Mediator.SourceGenerator` (PrivateAssets) + `Mediator.Abstractions`, `FluentValidation`, and `Riok.Mapperly`.
- DI registration in `EarlyEagles.Application` is unchanged from the plan's *shape* — there is still a `DependencyInjection.cs` extension that calls `services.AddMediator(...)` and `services.AddValidatorsFromAssembly(...)`. Pipeline-behavior task IDs (P0-024.1 → .4) stay valid; the per-task content is the same logic, just attached to `Mediator`'s `IPipelineBehavior<,>` (same interface name, same generic signature, same `Handle(message, next, ct)` shape).
- Mapper classes are concrete types (e.g. `internal sealed partial class MemberMapper`) decorated with `[Mapper]`; they are registered as scoped services in `DependencyInjection.cs` so handlers can inject them.

## Consequences

### Saved now
- Zero per-developer / per-seat license cost for the mediator + mapping layer, for as long as we stay on these picks.
- Source-generated mediator and mapper avoid reflection — measurable cold-start and per-request improvement (Mediator benchmarks ~25× lower allocations than MediatR; Mapperly maps faster than AutoMapper because the code is direct property assignment, not delegate trees).
- AOT-readiness preserved for future scenarios (Lambda-style serverless, AOT-compiled containers).

### Cost paid
- **Stack Overflow surface is smaller.** MediatR has years of accumulated Q&A; martinothamar/Mediator is younger. Mitigation: the API is intentionally MediatR-shaped, so most patterns translate directly. Context7 (per the project's "use Context7 for library docs" rule) has 296 snippets for `/martinothamar/mediator` and 481 for `/riok/mapperly` — consult before guessing.
- **Mapping ergonomics differ.** AutoMapper's runtime profile/convention model is replaced by explicit partial methods. Slightly more typing per mapping, fully type-checked at compile time. This is closer to the "manual mapping" style some Clean Architecture practitioners already prefer.
- **Pipeline-behavior signature is the same** (`IPipelineBehavior<TRequest, TResponse>`) but lives in the `Mediator` namespace, not `MediatR`. No conceptual change to P0-024.1..4 — they're "implement behavior, register it, write its test." The class is the same shape.

### Documentation impact
- [`CLAUDE.md`](../../CLAUDE.md) Operative-stack table row "Backend" gets a footnote pointing to this ADR for the Mediator + Mapping picks; the prose elsewhere referring to MediatR/AutoMapper is updated.
- [`Plan.md`](../../Plan.md) stack table gets the same correction.
- [`Implementation-Plan.md`](../../Implementation-Plan.md) task descriptions for P0-023.1 (Mediator install) and P0-023.3 (mapping library install) are updated to the new packages. P0-024.* descriptions stay valid — same pipeline-behavior concept, different namespace.
- [`PROGRESS.md`](../../PROGRESS.md) reflects the same.
- ADR-0001 and ADR-0002 stay as-is per the immutable-ADR convention; their MediatR / AutoMapper cells are now historical, superseded here.

### Risk acknowledged
- **Lock-in to the source-generator approach.** Both Mediator and Mapperly rely on Roslyn source generation. A future Roslyn or SDK breakage could in principle delay an upgrade. Mitigation: both projects track .NET SDK releases closely; .NET 10 SDK is already supported.
- **Vendor risk on tiny libraries.** martinothamar/Mediator is maintained by one person (Martin Othamar) and Mapperly by a small team. If either stalls, migrating *away* is harder than from a well-known commercial library. Mitigation: both are MIT, so worst case we fork. We will revisit this risk at the start of Phase 2 (commercialization) when the codebase is larger and migration cost is higher.
- **Future MediatR / AutoMapper version with a free tier.** If Lucky Penny Software introduces a generous OSS / startup tier, we may revisit. This ADR doesn't preclude that re-evaluation; it just isn't waiting on it.

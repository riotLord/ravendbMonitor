# Roadmap

## Objective

Evolve `Servercyde.Monitoring` from a RavenDB-specific monitoring solution into a provider-adaptable monitoring app while preserving the current production behavior.

The roadmap assumes:

- the app must keep working during the transition
- RavenDB remains the current provider for now
- the eventual goal is to support a lower-cost alternative without rewriting every project

## Phase 1. Document and Stabilize

Focus on clarity before major refactoring.

- confirm the current runtime architecture
- confirm `Reporter` as the orchestration center
- document RavenDB-specific dependencies and configuration
- keep the CI/CD path stable while refactoring begins
- use the diagrams and notes as the source of truth for the current shape

Expected outcome:

- the team understands the current dependency flow
- the code has a clearly identified orchestration layer and adapter layer

## Phase 2. Extract Stable Application Slices

Refactor toward vertical slices without changing behavior.

Recommended slices:

- `Alert Reporting`
- `Connectivity Probe`
- `Provider Integration`
- `Email Delivery`

Goals for this phase:

- keep trigger code thin
- keep orchestration in slice-level application services
- separate provider-neutral contracts from provider-specific implementations
- improve naming so RavenDB-specific details do not leak into generic orchestration code

Expected outcome:

- the app is structured around behavior rather than storage technology

## Phase 3. Introduce Provider Boundaries

Make the monitoring path adapter-friendly.

- review whether `IMonitor` is enough or should be expanded/refined
- isolate RavenDB HTTP calls and certificate logic behind one boundary
- isolate provider-specific connectivity probing
- prepare configuration so multiple providers could coexist during a migration period

Expected outcome:

- RavenDB becomes one adapter, not the implicit architecture of the app

## Phase 4. Migration Readiness

Prepare the app for an alternative backend/provider.

- define the minimum contract a replacement provider must support
- map each RavenDB-specific feature to an equivalent or fallback approach
- identify behavior that depends on RavenDB-specific endpoints or semantics
- decide whether the connectivity probe remains provider-specific or becomes generalized

Expected outcome:

- the app is technically ready for a second provider implementation

## Phase 5. Incremental Provider Replacement

Once the target replacement is chosen:

- implement the new provider adapter beside RavenDB
- validate the new provider against existing report-generation tests
- run side-by-side comparison where possible
- switch configuration and composition root gradually

Expected outcome:

- the app can move off RavenDB with controlled risk

## Architecture Guidance For The Update

### SOLID

- single responsibility: keep triggers, orchestration, adapters, and composition separate
- open/closed: add providers by adding adapters, not by rewriting `Reporter`
- Liskov substitution: new provider implementations must satisfy the same monitoring contract
- interface segregation: keep provider interfaces small and purpose-driven
- dependency inversion: depend on abstractions in the application layer, not concrete provider classes

### DRY

- centralize email summary construction rules
- avoid duplicating provider configuration mapping
- avoid repeating alert transformation logic across adapters
- keep shared models and shared test helpers in one place when truly common

### Vertical Slice

- group code by behavior first:
  - alert reporting
  - connectivity probing
  - provider integration
  - email delivery
- keep transport, application logic, and adapter code close to the slice they serve
- avoid spreading one feature across too many unrelated technical folders

## Success Criteria

- current behavior remains intact
- tests still pass
- RavenDB-specific code is isolated and named clearly
- the app can support an additional provider with limited function-layer changes
- the architecture is easier to reuse across other projects making the same provider transition

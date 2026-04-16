# Tasks

## Goal

Refactor `Servercyde.Monitoring` so the app keeps working while becoming easier to move away from RavenDB in the future.

The update should follow:

- `SOLID`
- `DRY`
- vertical slice architecture

## Principles For The Refactor

- Keep `Reporter` as the orchestration entry for the alert-reporting slice.
- Prefer small replaceable interfaces over broad concrete dependencies.
- Keep provider-specific behavior behind adapter boundaries.
- Avoid duplicating alert formatting, configuration mapping, or transport logic.
- Organize by feature/slice first, not by technical type only.

## Task List

### 1. Stabilize the current architecture

- Confirm `Reporter` is the application-service/orchestration entry for alert reporting.
- Keep function triggers thin and limited to transport concerns.
- Keep `Bootstrapper` limited to composition-root concerns.

### 2. Define provider-neutral seams

- Review `IMonitor` and decide whether it should become a more provider-neutral monitoring port.
- Identify which models are safe to keep provider-neutral:
  - `DatabaseSummary`
  - `DatabaseAlert`
- Identify which models/config are RavenDB-specific and should stay in a provider adapter boundary:
  - `RavenConfig`
  - certificate handling
  - RavenDB HTTP/query logic
  - connectivity probe details

### 3. Introduce vertical slices

- Define an `Alert Reporting` slice centered on `Reporter`.
- Define a `Connectivity Probe` slice centered on `IRavenDbConnectivityProbe`.
- Define a `Provider Adapter` slice for RavenDB-specific integration.
- Move shared abstractions to the smallest common boundary needed by those slices.

### 4. Reduce RavenDB coupling

- Isolate `RavenDbMonitor` so it is purely a RavenDB adapter.
- Isolate `RavenDbConnectivityProbe` so it is clearly provider-specific.
- Remove RavenDB naming from generic orchestration where possible.
- Prepare configuration so future providers can coexist without breaking the current app.

### 5. Clean up dependency flow

- Ensure dependency direction stays simple:
  - `Functions -> Application slice`
  - `Application slice -> Provider adapter`
  - `Application slice -> Email adapter`
- Avoid concrete implementation dependencies inside the trigger layer.
- Avoid putting provider-specific decisions into `Reporter`.

### 6. Protect behavior with tests

- Preserve current report-generation behavior.
- Preserve current all-clear vs alert email behavior.
- Preserve current exception-email behavior.
- Preserve current connectivity probe behavior.
- Preserve current CI/CD test gate expectations.

### 7. Prepare migration analysis

- Document every RavenDB-specific touchpoint in the app.
- Identify what a replacement provider would need to supply to satisfy the reporting flow.
- Identify which parts are easy to swap and which parts are tightly coupled today.

## Current High-Value Refactor Targets

- [Reporter.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Reporter.cs:13)
- [Bootstrapper.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Infrastructure\Bootstrapper.cs:14)
- [RavenDbMonitor.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbMonitor.cs:8)
- [RavenDbConnectivityProbe.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbConnectivityProbe.cs:11)
- [Triggers.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring.Functions\Triggers.cs:8)

## Done Criteria

- The reporting flow still works end-to-end.
- The connectivity probe still works end-to-end.
- RavenDB-specific code is easier to identify and replace.
- `Reporter` remains stable as the orchestration point.
- The codebase is easier to extend with another provider without rewriting the function layer.

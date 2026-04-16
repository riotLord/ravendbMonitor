# Diagram Notes

## Architecture Diagram Reviewed

- Diagram: `RavenDB Monitor Architecture (Reporter Orchestration)`
- Board link: `https://miro.com/app/board/uXjVGhqidv0=/?moveToWidget=3458764668067425560`

## What The Updated Architecture Shows

The updated architecture diagram now reflects the code more accurately than the earlier implementation-oriented versions, and it is moving toward a clearer dependency-oriented view.

At a high level, it shows four buckets:

- `Core`
- `Implementations`
- `Functions`
- `Tests`

The most important change is that `Reporter` is treated as the main orchestration class inside `Core`.

The flow shown by the updated diagram is now much closer to the real code:

- `CheckRavenDbAlertsHttp -> Reporter`
- `Reporter -> RavenDbMonitor`
- `Reporter -> EmailService`
- `CheckRavenDbConnectionHttp -> IRavenDbConnectivityProbe`

This better matches the actual entrypoint and dependency flow in:

- [Triggers.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring.Functions\Triggers.cs:8)
- [Reporter.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Reporter.cs:13)
- [Bootstrapper.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Infrastructure\Bootstrapper.cs:14)

## Architecture Changes Made

Compared with the earlier versions from this thread, the updated architecture appears to make these changes:

- removes the incorrect MediatR idea
- makes `Reporter` the central class in `Core`
- shows `Reporter` as the class that coordinates the alert flow
- shows `RavenDbMonitor` and `EmailService` as downstream implementation dependencies
- keeps the connectivity path separate through `IRavenDbConnectivityProbe`
- keeps the diagram bucketed instead of showing many cross-system runtime links
- shifts the picture away from generic “layers” and closer to the actual dependency pattern in the app
- clarifies that the important runtime path is driven by the alert trigger and the reporter orchestration path, not by a mediator pipeline

## Why These Architecture Changes Were Likely Made

These changes seem intended to align the diagram with the actual code rather than with a generalized architecture pattern.

- Removing MediatR likely happened because this app does not use that pattern at all.
- Moving `Reporter` into the center likely happened because it is the actual application-service style orchestrator used by the function trigger.
- Separating the connectivity probe path likely happened because it is not part of the normal reporting flow.
- Keeping the architecture bucketed likely happened to make it easier to understand the codebase at a glance without introducing too many connector lines.
- Showing only the important relationships likely makes the diagram more maintainable as the code evolves.
- Introducing clearer implementation and interface boundaries likely prepares the project for a future database/provider migration.

## What Improved In The Architecture

- The diagram now describes the real code path more faithfully.
- `Reporter` is finally shown as the main coordinator, which is the most important architectural truth in this app.
- The two function triggers are easier to understand as different entrypoints with different responsibilities.
- The service relationships are clearer:
  - alert trigger uses `Reporter`
  - `Reporter` uses monitoring and email services
  - connection trigger uses the connectivity probe
- The diagram is simpler and easier to use as a refactor guide.
- The current shape is more useful for thinking about a future provider swap because it highlights the orchestration seam and the concrete implementation seam separately.

## Additional Notes From The Latest Update

Based on the current board state, the latest changes appear to emphasize interface and orchestration boundaries more than storage or hosting details.

- The diagram now appears to focus on the alert orchestration path as the primary story.
- The implementation bucket is being used to express the replaceable service side of the application.
- The connectivity probe remains isolated, which is useful because it is operationally important but not part of the standard reporting pipeline.
- The tests area still reads as supporting verification rather than runtime behavior, which is appropriate.

The main architectural gap that still matters for future refactoring is that RavenDB-specific concerns are still grouped mostly by implementation class rather than by a more provider-neutral slice. That is not wrong for the current app, but it is the place that will matter most when migration work begins.

## Refactor Readiness Notes

If the long-term goal is to move away from RavenDB across multiple projects, this app already has a useful seam, but it is not fully datastore-agnostic yet.

The good news:

- `Reporter` already depends on `IMonitor`, not directly on `RavenDbMonitor`
- the function trigger depends on `IReporter`, not directly on database code
- the app already separates orchestration from concrete database access reasonably well

The current RavenDB-specific coupling is concentrated in a few places:

- [RavenDbMonitor.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbMonitor.cs:8)
- [RavenDbConnectivityProbe.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbConnectivityProbe.cs:11)
- [RavenDbServiceExtensions.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbServiceExtensions.cs:13)
- [Config.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Config.cs:13)
- [Bootstrapper.cs](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Infrastructure\Bootstrapper.cs:30)

The likely changes needed to make this app portable are:

- keep `Reporter` as the stable orchestration layer
- replace or generalize `IMonitor` so it represents a monitoring data source rather than a RavenDB-specific implementation detail
- isolate RavenDB-specific HTTP calls, certificate handling, and query logic behind one implementation boundary
- decide whether connectivity probing remains provider-specific or becomes a more generic health probe abstraction
- separate datastore-neutral report models from provider-specific data acquisition concerns
- reduce direct reliance on RavenDB configuration names in the composition root if a future provider must coexist during migration
- reorganize the code toward feature- or slice-oriented boundaries so alert reporting, connectivity probing, and provider adapters do not bleed into each other

In practical terms, this app is already closest to a pattern like:

- `Functions` = entrypoints
- `Reporter` = orchestration / application service
- `IMonitor` and `IEmailService` = ports
- `RavenDbMonitor`, `EmailService`, `RavenDbConnectivityProbe` = adapters / implementations

That means the safest migration path is probably not to rewrite `Reporter`, but to preserve it and replace the RavenDB adapter layer under it.

## Diagram Reviewed

- Diagram: `RavenDB Monitor CI/CD Flow (Clean)`
- Board link: `https://miro.com/app/board/uXjVGhqidv0=/?moveToWidget=3458764668059167685`
- Repo workflow used for verification: `.github/workflows/deploy-functionapp.yml`

## What The Diagram Shows

The diagram shows the GitHub Actions deployment flow for the RavenDB Monitor Azure Function App.

It currently presents this flow:

- `Push to main or manual dispatch`
- `Checkout`
- `Setup .NET 9, restore, and build`
- `Run tests excluding IntegrationTest`
- `Tests pass?`
- `Deploy to Azure Functions`
- `scnet-rdb-mon Function App`

It also shows the deployment inputs as a separate supporting group:

- `Publish profile secret`
- `AzureKeyVault app setting`
- `Managed identity access`
- `Key Vault secrets`

This matches the real workflow at a high level:

- the workflow runs on push to `main` or manual dispatch
- it checks out the repo
- it sets up .NET 9
- it restores dependencies
- it builds the solution
- it runs tests with `Category!=IntegrationTest`
- it deploys to Azure Functions only after tests succeed

## Changes Made

Compared with the previous generated CI/CD version from this thread, the updated diagram appears to make these changes:

- `Setup .NET 9`, `Restore`, and `Build` are consolidated into one block
- the main pipeline is kept in a cleaner left-to-right sequence
- the legacy Azure DevOps context is removed
- deployment inputs remain separated from the main execution path
- the `Tests pass?` gate is still preserved before deployment
- the final target remains the deployed Azure Function App

## Why These Changes Were Likely Made

These changes seem intended to improve readability rather than change the meaning.

- Combining setup, restore, and build likely reduces visual clutter and shortens the main pipeline.
- Removing the legacy Azure DevOps context likely keeps attention on the real deployment path that matters now.
- Keeping the deployment inputs in a separate group likely helps distinguish prerequisites from the main CI/CD flow.
- Keeping the test gate explicit likely reinforces that deployment depends on successful test execution.
- Keeping the flow left-to-right likely makes it easier to understand at a glance in a browser view.

## What Improved

Several parts of the diagram are clearer in the updated version.

- The main CI/CD path is easier to scan quickly.
- The pipeline now emphasizes the current GitHub Actions flow instead of mixing in older context.
- The deployment gate is still obvious.
- The deployment prerequisites are visible without crowding the main sequence.
- The diagram is more presentation-friendly for a quick technical overview.

## Remaining Notes

The updated version is noticeably cleaner, but there are still a few small points worth noting.

- The combined `Setup .NET 9, restore, and build` block is clearer visually, but it hides the fact that these are three separate workflow steps in the actual YAML.
- The diagram does not show an explicit `NO` path from `Tests pass?`, which is probably fine for a clean overview, but it means the failure behavior is implied rather than shown.
- The deployment input connections may still need minor visual adjustment depending on how precise the board layout needs to be, but structurally the separation is clearer than before.
- The Function App node is clear as the deployment target, but the Azure-side configuration inputs are best read as environment prerequisites rather than steps in the pipeline itself.

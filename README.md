# Servercyde.Monitoring

`Servercyde.Monitoring` checks RavenDB servers for database alerts and operational issues, then sends an HTML email summary through Azure Communication Services Email. The solution includes a reusable core library, an Azure Functions host, and test projects covering the monitoring and reporting flow.

## Solution layout

- `Servercyde.Monitoring` contains the core monitoring, reporting, email, and dependency registration code.
- `Servercyde.Monitoring.Functions` hosts the Azure Functions entry point and wires configuration into the core services.
- `Servercyde.Monitoring.Tests` provides shared test helpers and fakes.
- `Servercyde.Monitoring.Core.Tests` covers the core library behavior.
- `Servercyde.Monitoring.Functions.Tests` covers the function trigger surface.
- `RavenDB` contains sample response payloads used by the test suite.

## How it works

1. The Functions host starts in [`Servercyde.Monitoring.Functions/Program.cs`](D:\DEV\ravendbMonitor\Servercyde.Monitoring.Functions\Program.cs) and calls the bootstrapper in the core project.
2. [`Bootstrapper`](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Infrastructure\Bootstrapper.cs) binds configuration, optionally loads secrets from Azure Key Vault, and registers the RavenDB and Azure Communication Services email services.
3. [`Triggers`](D:\DEV\ravendbMonitor\Servercyde.Monitoring.Functions\Triggers.cs) exposes the compatibility-sensitive HTTP function `CheckRavenDbAlertsHttp`.
4. [`Reporter`](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Reporter.cs) gathers summaries from RavenDB, builds an HTML report, and sends the result by email.
5. [`RavenDbMonitor`](D:\DEV\ravendbMonitor\Servercyde.Monitoring\Database\RavenDbMonitor.cs) pulls database lists, notifications, and zero-retry queued commands from each configured RavenDB server.

## Configuration

The current code expects these configuration sections and environment values:

- `Monitor`
  - `ToEmail`
  - `FromEmail`
  - `EmailSubject`
- `RavenDB`
  - `Urls`
  - `CertificateThumbprint`
  - `CertificateBase64`
  - `CertificatePassword`
  - `DatabaseIncludes`
  - `DatabaseExcludes`
- `AzureCommunicationServices`
  - `ConnectionString`
- `AzureKeyVault`
  - environment variable containing the Azure Key Vault name to load secrets from

These keys are intentionally preserved for compatibility with existing configuration and integrations. For Azure-hosted deployments, `CertificateBase64` plus `CertificatePassword` is the simplest path when the RavenDB client certificate is stored in Key Vault instead of on the local filesystem or certificate store.

The current email configuration assumes an Azure-managed ACS sender address is stored in `Monitor--FromEmail`.

For local test configuration, see [`Servercyde.Monitoring.Core.Tests/appsettings.test.json`](D:\DEV\ravendbMonitor\Servercyde.Monitoring.Core.Tests\appsettings.test.json).

## Running locally

Build the solution:

```powershell
dotnet build .\Servercyde.Monitoring.sln
```

Run the tests:

```powershell
dotnet test .\Servercyde.Monitoring.sln
```

Run the Azure Functions host:

```powershell
dotnet run --project .\Servercyde.Monitoring.Functions\Servercyde.Monitoring.Functions.csproj
```

The HTTP function accepts an optional `profile` query string. Passing `profile=simulate` uses generated fake data so the reporting flow can be exercised without a live RavenDB instance.

## CI

[`azure-pipelines.yml`](D:\DEV\ravendbMonitor\azure-pipelines.yml) remains as a legacy Azure DevOps build definition. GitHub Actions is the intended deployment path for the Azure Function App.

## Deploying To Azure

The GitHub Actions deployment workflow is [deploy-functionapp.yml](D:\DEV\ravendbMonitor\.github\workflows\deploy-functionapp.yml). It targets the `scnet-rdb-mon` Flex Consumption Function App on pushes to `main`.

Before the workflow can deploy successfully, configure:

- GitHub secret `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
- Function App app setting `AzureKeyVault = scnet-key-vault`
- Function App managed identity with Key Vault secret read access
- Key Vault secrets:
  - `AzureCommunicationServices--ConnectionString`
  - `Monitor--FromEmail`
  - `Monitor--ToEmail`
  - `Monitor--EmailSubject`
  - `RavenDB--Urls--0`
  - `RavenDB--CertificateBase64`
  - `RavenDB--CertificatePassword`

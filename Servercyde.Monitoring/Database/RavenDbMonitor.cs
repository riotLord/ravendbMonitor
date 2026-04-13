using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Servercyde.Monitoring.Core.Database;

public class RavenDbMonitor(IHttpClientFactory httpClientFactory, IOptions<RavenConfig> options) : IMonitor
{
    #region Raven API DTOs
    private sealed record DatabaseResponse(List<DatabaseSummary>? Databases);
    private sealed record AlertsResponse(List<DatabaseAlert>? Results);
    #endregion


    private HttpClient HttpClient => httpClientFactory.CreateClient("RavenDB");

    public async Task<DatabaseSummary[]> GetSummaries(
        string profile,
        CancellationToken cancellationToken
    )
    {
        var shouldFakeData = string.Compare(profile, "simulate", StringComparison.OrdinalIgnoreCase) == 0;
        if (shouldFakeData)
        {
            return FakeDatabaseSummariesFactory.Create(10);
        }
        var ravenConfig = options.Value;

        var fetchSummariesTask = ravenConfig.Urls.Select(url => FetchSummaries(url, cancellationToken));

        var fetchedSummaries = await Task.WhenAll(fetchSummariesTask);

        var summariesWithAlerts = (
            await AddAlertsToSummaries(
                FilterSummaries(
                    [.. fetchedSummaries.SelectMany(x => x)], 
                    ravenConfig.DatabaseIncludes, 
                    ravenConfig.DatabaseExcludes), 
                    cancellationToken))
            .ToArray();
        return summariesWithAlerts;
    }

    private static IEnumerable<DatabaseSummary> FilterSummaries(List<DatabaseSummary> summaries, string[] includePattern, string[] excludePattern)
    {
        var matcher = new Matcher();

        switch (includePattern.Length)
        {
            case 0:
                matcher.AddInclude("*");
                break;
            default:
                matcher.AddIncludePatterns(includePattern);
                break;
        }

        if (excludePattern.Length != 0)
            matcher.AddExcludePatterns(excludePattern);

        var result = matcher.Match(summaries.Select(x => x.Name));

        return summaries
            .Where(x =>
                result.Files.Any(file => file.Path == x.Name));
    }

    private async Task<IEnumerable<DatabaseSummary>> FetchSummaries(string server, CancellationToken cancellationToken = default)
    {
        HttpClient.BaseAddress = new Uri(server);
        var response = await HttpClient.GetAsync("/databases", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<DatabaseResponse>(cancellationToken);

        return content?.Databases?.Select(x => x with { Server = server }) ?? [];
    }

    public async Task<IEnumerable<DatabaseSummary>> AddAlertsToSummaries(
        IEnumerable<DatabaseSummary> databaseSummaries,
        CancellationToken cancellationToken
    )
    {        
        var updatedSummariesTasks = databaseSummaries
            .Select(x=>AddAlertsToSummary(x,cancellationToken));
        var updatedSummaries = await Task.WhenAll(updatedSummariesTasks);
        return updatedSummaries;
    }

    private async Task<IEnumerable<DatabaseAlert>> GetDatabaseAlerts(string databaseServer, string databaseName, CancellationToken cancellationToken)
    {
        try
        {
            HttpClient.BaseAddress = new Uri(databaseServer);
            var response = await HttpClient.GetAsync($"/databases/{databaseName}/notifications", cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<AlertsResponse>(cancellationToken);
            return content?.Results?.Select(x => x with { Server = databaseServer }) ?? [];
        }
        catch
        {
            // Do not care currently
        }
        return [];
    }

    public async Task<IEnumerable<DatabaseAlert>> GetQueuedCommandsWithZeroRetriesRemaining(
    string databaseServerName,
    string databaseName,
    CancellationToken cancellationToken = default)
    {
        var queryString = "from QueuedCommand where RetriesRemaining = 0";
        var url = $"/databases/{databaseName}/queries?query={Uri.EscapeDataString(queryString)}";
        
        HttpClient.BaseAddress = new Uri(databaseServerName);
        
        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);

        // Try to get results from the document if empty return empty list
        if (!doc.RootElement.TryGetProperty("Results", out var results)) return [];

        var alerts = new List<DatabaseAlert>();
        foreach (var item in results.EnumerateArray())
        {
            var docId = item.GetProperty("@metadata").GetProperty("@id").GetString() ?? "UnknownId";

            alerts.Add(
                new DatabaseAlert(
                    databaseServerName,
                    databaseName,
                    docId,
                    "QueuedCommand Retries = 0",
                    "A QueuedCommand has run out of retries.",
                    DateTime.UtcNow,
                    "RetryFailure",
                    DatabaseAlert.SeverityLevel.Critical)                
            );
        }
        return alerts;
    }

    private async Task<DatabaseSummary> AddAlertsToSummary(
        DatabaseSummary db,
        CancellationToken cancellationToken
    )
    {
        var zeroRetryAlerts = await GetQueuedCommandsWithZeroRetriesRemaining(db.Server, db.Name, cancellationToken);

        var dbAlerts = await GetDatabaseAlerts(db.Server, db.Name, CancellationToken.None);
        var updatedSummary = db with { Alerts = UpdateAlertsSource([.. zeroRetryAlerts, .. dbAlerts]) };
        return updatedSummary;
    }

    private static DatabaseAlert[] UpdateAlertsSource(IEnumerable<DatabaseAlert> alerts) => [.. alerts
            .Select(alert => 
                        alert with { 
                            Server = $"{alert.Server}studio/index.html#databases/documents?&database={alert.Database}" 
                    })];
    
}

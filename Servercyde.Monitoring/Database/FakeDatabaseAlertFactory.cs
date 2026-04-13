using System.Security.Cryptography;

namespace Servercyde.Monitoring.Core.Database;

public static class FakeDatabaseAlertFactory 
{
    public static readonly string[] AllAlertTitles = ["High CPU Usage", "Index Stalled", "Memory Leak", "Disk Full", "Network Error"];
    public static readonly string[] AllAlertCategories = ["Performance", "Indexing", "Resource", "Storage", "Connectivity"];

    public static IEnumerable<DatabaseAlert> Create(string serverName, string dbName, int count)
    {
        return Enumerable.Range(1, count).Select(i =>
        {
            var alertId = $"alert/{i}";
            var title = AllAlertTitles[RandomNumberGenerator.GetInt32(AllAlertTitles.Length)];
            var message = $"Simulated alert message for {title} in {dbName}";
            var createdAt = DateTime.UtcNow.AddMinutes(-RandomNumberGenerator.GetInt32(60));
            var category = AllAlertCategories[RandomNumberGenerator.GetInt32(AllAlertCategories.Length)];
            var severity = DatabaseAlert.AllSeverities[RandomNumberGenerator.GetInt32(DatabaseAlert.AllSeverities.Length)];

            return new DatabaseAlert(serverName, dbName, alertId, title, message, createdAt, category, severity);
        });
    }
}


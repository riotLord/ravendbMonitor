using System.Security.Cryptography;
namespace Servercyde.Monitoring.Core.Database;

public static class FakeDatabaseSummariesFactory
{
    public static DatabaseSummary[] Create(int count)
    {
         return [.. Enumerable.Range(1, count)
            .Select(i =>
            {
                var name = $"TestDB{i}";
                var server = $"server{i}.example.com";
                var alerts = FakeDatabaseAlertFactory.Create(server, name, 2).ToArray();
                return new DatabaseSummary(server, name, 2, RandomNumberGenerator.GetInt32(100, 1000), 2,3) { Alerts = alerts };
            })];
    }
}


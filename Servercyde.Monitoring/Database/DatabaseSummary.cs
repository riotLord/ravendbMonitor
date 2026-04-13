using System.Text.Json.Serialization;
namespace Servercyde.Monitoring.Core.Database;

public record DatabaseSummary(
    string Server,
    string Name,
    int AlertsCount,
    int PerformanceHintsCount, 
    long DocumentsCount,
    long IndexesCount
)
{
    [JsonIgnore]
    public DatabaseAlert[] Alerts { get; init; } = [];
};


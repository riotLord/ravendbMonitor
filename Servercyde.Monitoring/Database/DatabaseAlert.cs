using System.Text.Json.Serialization;

namespace Servercyde.Monitoring.Core.Database;

public record DatabaseAlert(
    string Server,
    string Database,
    string Id,
    string Title,
    string Message,
    DateTime CreatedAt,
    string Category,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DatabaseAlert.SeverityLevel Severity
)
{
    public enum SeverityLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public static readonly SeverityLevel[] AllSeverities = [
        SeverityLevel.Info,
        SeverityLevel.Warning,
        SeverityLevel.Error,
        SeverityLevel.Critical
    ];


    public string Source() => $"{Server}/studio/index.html?&databases/documents?&database={Database}";

    public string FriendlyServerSubDomainName => Server.Contains("https://") ? Server.Split("https://")[1].Split(".")[0] : Server.Split(".")[0];


}




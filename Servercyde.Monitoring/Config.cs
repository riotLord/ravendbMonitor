namespace Servercyde.Monitoring.Core;

public class MonitorConfig
{
    public const string KEY = "Monitor";
    public string ToEmail { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string EmailSubject { get; set; } = "";


}

public class RavenConfig {
    public const string KEY = "RavenDB";

    public string[] Urls { get; set; } = [];

    public string? CertificateThumbprint { get; set; } = null;
    public string? CertificateBase64 { get; set; } = null;
    public string? CertificatePassword { get; set; } = null;
    public string[] DatabaseExcludes { get; set; } = [];

    public string[] DatabaseIncludes { get; set; } = [];
}

public class AzureCommunicationServicesConfig
{
    public const string KEY = "AzureCommunicationServices";

    public string ConnectionString { get; set; } = "";
}


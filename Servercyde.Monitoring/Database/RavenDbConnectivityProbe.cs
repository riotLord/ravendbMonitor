using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Servercyde.Monitoring.Core.Database;

public interface IRavenDbConnectivityProbe
{
    Task<RavenDbConnectivityProbeResult> Probe(CancellationToken cancellationToken = default);
}

public class RavenDbConnectivityProbe(
    IHttpClientFactory httpClientFactory,
    IOptions<RavenConfig> options,
    RavenDbCertificateDiagnostics certificateDiagnostics,
    ILogger<RavenDbConnectivityProbe> logger) : IRavenDbConnectivityProbe
{
    public async Task<RavenDbConnectivityProbeResult> Probe(CancellationToken cancellationToken = default)
    {
        var primaryUrl = options.Value.Urls.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(primaryUrl))
        {
            var missingConfigResult = new RavenDbConnectivityProbeResult(
                ConfigurationLoaded: false,
                CertificateConfigured: certificateDiagnostics.CertificateConfigured,
                CertificateLoaded: certificateDiagnostics.CertificateLoaded,
                PrivateKeyPresent: certificateDiagnostics.PrivateKeyPresent,
                TlsRequestSucceeded: false,
                HttpStatusCode: null,
                RavenDbUrl: null,
                CertificateThumbprint: certificateDiagnostics.Thumbprint,
                CertificateSubject: certificateDiagnostics.Subject,
                CertificateExpiresUtc: certificateDiagnostics.NotAfterUtc,
                CertificateLoadStrategy: certificateDiagnostics.KeyStorageMode,
                CertificateSource: certificateDiagnostics.Source,
                FailureStage: "Configuration",
                ExceptionType: nameof(InvalidOperationException),
                ExceptionMessage: "No RavenDB URL is configured.",
                InnerExceptionType: null,
                InnerExceptionMessage: null);
            logger.LogError("RavenDB connectivity probe failed because no RavenDB URL is configured.");
            return missingConfigResult;
        }

        try
        {
            var client = httpClientFactory.CreateClient(RavenDbServiceExtensions.HTTP_CLIENT_NAME);
            var response = await client.GetAsync("/databases", cancellationToken);
            var result = new RavenDbConnectivityProbeResult(
                ConfigurationLoaded: true,
                CertificateConfigured: certificateDiagnostics.CertificateConfigured,
                CertificateLoaded: certificateDiagnostics.CertificateLoaded,
                PrivateKeyPresent: certificateDiagnostics.PrivateKeyPresent,
                TlsRequestSucceeded: response.IsSuccessStatusCode,
                HttpStatusCode: (int)response.StatusCode,
                RavenDbUrl: primaryUrl,
                CertificateThumbprint: certificateDiagnostics.Thumbprint,
                CertificateSubject: certificateDiagnostics.Subject,
                CertificateExpiresUtc: certificateDiagnostics.NotAfterUtc,
                CertificateLoadStrategy: certificateDiagnostics.KeyStorageMode,
                CertificateSource: certificateDiagnostics.Source,
                FailureStage: response.IsSuccessStatusCode ? null : "HttpRequest",
                ExceptionType: null,
                ExceptionMessage: response.IsSuccessStatusCode
                    ? null
                    : $"RavenDB probe returned HTTP {(int)response.StatusCode}.",
                InnerExceptionType: null,
                InnerExceptionMessage: null);

            logger.LogInformation(
                "RavenDB connectivity probe completed. Success={Success}, HttpStatusCode={HttpStatusCode}, Url={RavenDbUrl}",
                result.Success,
                result.HttpStatusCode,
                result.RavenDbUrl);
            return result;
        }
        catch (Exception ex)
        {
            var result = new RavenDbConnectivityProbeResult(
                ConfigurationLoaded: true,
                CertificateConfigured: certificateDiagnostics.CertificateConfigured,
                CertificateLoaded: certificateDiagnostics.CertificateLoaded,
                PrivateKeyPresent: certificateDiagnostics.PrivateKeyPresent,
                TlsRequestSucceeded: false,
                HttpStatusCode: null,
                RavenDbUrl: primaryUrl,
                CertificateThumbprint: certificateDiagnostics.Thumbprint,
                CertificateSubject: certificateDiagnostics.Subject,
                CertificateExpiresUtc: certificateDiagnostics.NotAfterUtc,
                CertificateLoadStrategy: certificateDiagnostics.KeyStorageMode,
                CertificateSource: certificateDiagnostics.Source,
                FailureStage: "TlsHandshake",
                ExceptionType: ex.GetType().Name,
                ExceptionMessage: ex.Message,
                InnerExceptionType: ex.InnerException?.GetType().Name,
                InnerExceptionMessage: ex.InnerException?.Message);

            logger.LogError(
                ex,
                "RavenDB connectivity probe failed during TLS/authentication. Url={RavenDbUrl}, ExceptionType={ExceptionType}, Message={ExceptionMessage}, InnerExceptionType={InnerExceptionType}, InnerExceptionMessage={InnerExceptionMessage}",
                result.RavenDbUrl,
                result.ExceptionType,
                result.ExceptionMessage,
                result.InnerExceptionType,
                result.InnerExceptionMessage);
            return result;
        }
    }
}

public sealed record RavenDbConnectivityProbeResult(
    bool ConfigurationLoaded,
    bool CertificateConfigured,
    bool CertificateLoaded,
    bool PrivateKeyPresent,
    bool TlsRequestSucceeded,
    int? HttpStatusCode,
    string? RavenDbUrl,
    string? CertificateThumbprint,
    string? CertificateSubject,
    DateTime? CertificateExpiresUtc,
    string? CertificateLoadStrategy,
    string? CertificateSource,
    string? FailureStage,
    string? ExceptionType,
    string? ExceptionMessage,
    string? InnerExceptionType,
    string? InnerExceptionMessage)
{
    public bool Success => ConfigurationLoaded
        && (!CertificateConfigured || (CertificateLoaded && PrivateKeyPresent))
        && TlsRequestSucceeded;
}

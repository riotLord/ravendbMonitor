using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servercyde.Monitoring.Core.Infrastructure;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace Servercyde.Monitoring.Core.Database;

public static class RavenDbServiceExtensions
{
    public const string HTTP_CLIENT_NAME = "RavenDB";

    public static void AddRavenDb(
        this IServiceCollection services,
        RavenConfig ravenConfig,
        HttpMessageHandler? interceptor = null,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(ravenConfig);

        if (ravenConfig.Urls.Length == 0 || string.IsNullOrEmpty(ravenConfig.Urls[0]))
        {
            throw new ArgumentNullException(nameof(ravenConfig), "Urls must have at least one URL specified");
        }

        logger?.LogInformation(
            "RavenDB certificate source configured as {CertificateSource}",
            GetCertificateSource(ravenConfig));

        var certificateRegistration = LoadCertificate(ravenConfig, logger);
        services.AddSingleton(certificateRegistration.Diagnostics);

        services.AddHttpClient(HTTP_CLIENT_NAME, config =>
            {
                config.BaseAddress = new Uri(ravenConfig.Urls[0]);
                config.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                CreatePrimaryHttpMessageHandler(interceptor, certificateRegistration.Certificate, logger));

        services.AddScoped<IMonitor, RavenDbMonitor>();
        services.AddScoped<RavenDbMonitor>();
    }

    internal static HttpMessageHandler CreatePrimaryHttpMessageHandler(
        HttpMessageHandler? interceptor,
        X509Certificate2? cert,
        ILogger? logger = null)
    {
        if (interceptor != null)
        {
            return interceptor;
        }

        var httpClientHandler = new HttpClientHandler();
        if (cert != null)
        {
            httpClientHandler.ClientCertificates.Add(cert);
            logger?.LogInformation(
                "RavenDB HTTP client configured with client certificate {Thumbprint}",
                cert.Thumbprint);
        }

        return httpClientHandler;
    }

    private static RavenDbCertificateRegistration LoadCertificate(RavenConfig ravenConfig, ILogger? logger)
    {
        if (!string.IsNullOrWhiteSpace(ravenConfig.CertificateBase64))
        {
            return CreateRegistration(
                CertificateLoader.LoadFromBase64WithDiagnostics(
                    ravenConfig.CertificateBase64,
                    ravenConfig.CertificatePassword,
                    logger),
                certificateConfigured: true);
        }

        if (!string.IsNullOrWhiteSpace(ravenConfig.CertificateThumbprint))
        {
            return CreateRegistration(
                CertificateLoader.LoadWithDiagnostics(
                    ravenConfig.CertificateThumbprint,
                    ravenConfig.CertificatePassword,
                    logger),
                certificateConfigured: true);
        }

        return new RavenDbCertificateRegistration(
            null,
            new RavenDbCertificateDiagnostics(
                CertificateConfigured: false,
                CertificateLoaded: false,
                PrivateKeyPresent: false,
                Thumbprint: null,
                Subject: null,
                NotAfterUtc: null,
                KeyStorageMode: null,
                Source: "None"));
    }

    private static RavenDbCertificateRegistration CreateRegistration(
        CertificateLoadResult loadResult,
        bool certificateConfigured)
    {
        if (!loadResult.Certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"RavenDB client certificate {loadResult.Certificate.Thumbprint} was loaded without a private key. Mutual TLS cannot succeed without a private key.");
        }

        return new RavenDbCertificateRegistration(
            loadResult.Certificate,
            new RavenDbCertificateDiagnostics(
                CertificateConfigured: certificateConfigured,
                CertificateLoaded: true,
                PrivateKeyPresent: loadResult.Certificate.HasPrivateKey,
                Thumbprint: loadResult.Certificate.Thumbprint,
                Subject: loadResult.Certificate.Subject,
                NotAfterUtc: loadResult.Certificate.NotAfter,
                KeyStorageMode: loadResult.KeyStorageMode,
                Source: loadResult.Source));
    }

    private static string GetCertificateSource(RavenConfig ravenConfig)
    {
        if (!string.IsNullOrWhiteSpace(ravenConfig.CertificateBase64))
        {
            return "Base64";
        }

        return string.IsNullOrWhiteSpace(ravenConfig.CertificateThumbprint)
            ? "None"
            : "Thumbprint";
    }
}

public sealed record RavenDbCertificateDiagnostics(
    bool CertificateConfigured,
    bool CertificateLoaded,
    bool PrivateKeyPresent,
    string? Thumbprint,
    string? Subject,
    DateTime? NotAfterUtc,
    string? KeyStorageMode,
    string Source);

internal sealed record RavenDbCertificateRegistration(
    X509Certificate2? Certificate,
    RavenDbCertificateDiagnostics Diagnostics);

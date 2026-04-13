using Microsoft.Extensions.DependencyInjection;
using Servercyde.Monitoring.Core.Infrastructure;

namespace Servercyde.Monitoring.Core.Database;

public static class RavenDbServiceExtensions
{
    public const string HTTP_CLIENT_NAME = "RavenDB";
    public static void AddRavenDb(
        this IServiceCollection services, 
        RavenConfig ravenConfig,
        HttpMessageHandler? interceptor = null
    )
    {
        ArgumentNullException.ThrowIfNull(ravenConfig);

        if (ravenConfig.Urls.Length == 0 || string.IsNullOrEmpty(ravenConfig.Urls[0]))
            throw new ArgumentNullException(nameof(ravenConfig), "Urls must have at least one URL specified");

        var cert = LoadCertificate(ravenConfig);
        
        services.AddHttpClient("RavenDB", config => {
                config.BaseAddress = new Uri(ravenConfig.Urls[0]);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                if (interceptor != null) return interceptor;

                var httpClientHandler = new HttpClientHandler();
                if (cert != null) httpClientHandler.ClientCertificates.Add(cert);
                return httpClientHandler;
            });

        services.AddScoped<IMonitor, RavenDbMonitor>();
        services.AddScoped<RavenDbMonitor>();
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2? LoadCertificate(RavenConfig ravenConfig)
    {
        if (!string.IsNullOrWhiteSpace(ravenConfig.CertificateBase64))
        {
            return CertificateLoader.LoadFromBase64(
                ravenConfig.CertificateBase64,
                ravenConfig.CertificatePassword);
        }

        return string.IsNullOrWhiteSpace(ravenConfig.CertificateThumbprint)
            ? null
            : CertificateLoader.Load(ravenConfig.CertificateThumbprint, ravenConfig.CertificatePassword);
    }
}


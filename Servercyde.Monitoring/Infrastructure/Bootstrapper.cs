using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Communication.Email;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Core.Email;

namespace Servercyde.Monitoring.Core.Infrastructure;

public static class Bootstrapper
{
    public static void ConfigureServices(
        ILogger logger,
        IServiceCollection services, 
        IConfigurationBuilder configurationBuilder,
        HttpMessageHandler? httpInterceptor = null
    )
    {
        var keyVaultName = Environment.GetEnvironmentVariable("AzureKeyVault");
        AddKeyVaultConfiguration(logger, configurationBuilder, keyVaultName);

        var configuration = configurationBuilder.Build();
        services.Configure<MonitorConfig>(configuration.GetSection(MonitorConfig.KEY));
        services.Configure<AzureCommunicationServicesConfig>(configuration.GetSection(AzureCommunicationServicesConfig.KEY));

        var ravenConfigSection = configuration.GetSection(RavenConfig.KEY);
        services.Configure<RavenConfig>(ravenConfigSection);

        var ravenConfig = new RavenConfig(); ravenConfigSection.Bind(ravenConfig);
        LogConfigurationPresence(logger, configuration, ravenConfig);
        services.AddRavenDb(ravenConfig, httpInterceptor);
        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IOptions<AzureCommunicationServicesConfig>>().Value.ConnectionString;
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            return new EmailClient(connectionString);
        });

        services.AddSingleton<IEmailClient, AzureCommunicationServicesEmailClient>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IReporter, Reporter>();
    }

    private static void LogConfigurationPresence(
        ILogger logger,
        IConfiguration configuration,
        RavenConfig ravenConfig)
    {
        logger.LogInformation(
            "Config diagnostics: AzureKeyVault set={HasKeyVault}, RavenDB urls count={RavenUrlCount}, RavenDB url[0] set={HasPrimaryRavenUrl}, RavenDB cert base64 set={HasCertificateBase64}, RavenDB cert password set={HasCertificatePassword}, ACS connection string set={HasAcsConnectionString}, Monitor from email set={HasFromEmail}, Monitor to email set={HasToEmail}, Monitor subject set={HasEmailSubject}",
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AzureKeyVault")),
            ravenConfig.Urls.Length,
            !string.IsNullOrWhiteSpace(configuration["RavenDB:Urls:0"]),
            !string.IsNullOrWhiteSpace(configuration["RavenDB:CertificateBase64"]),
            !string.IsNullOrWhiteSpace(configuration["RavenDB:CertificatePassword"]),
            !string.IsNullOrWhiteSpace(configuration["AzureCommunicationServices:ConnectionString"]),
            !string.IsNullOrWhiteSpace(configuration["Monitor:FromEmail"]),
            !string.IsNullOrWhiteSpace(configuration["Monitor:ToEmail"]),
            !string.IsNullOrWhiteSpace(configuration["Monitor:EmailSubject"]));
    }

    public static void AddKeyVaultConfiguration(
        ILogger logger, 
        IConfigurationBuilder configurationBuilder, 
        string? keyVaultName
    )
    {
        if (keyVaultName == null)
        {
            logger.LogWarning("AzureKeyVault not set. No setting will be loaded from a KeyVault");
        }
        else
        {
            try
            {
                logger.LogInformation("Loading config from AzureKeyVault {KeyVaultName}", keyVaultName);
                configurationBuilder.AddAzureKeyVault(
                        new Uri($"https://{keyVaultName}.vault.azure.net/"),
                        new DefaultAzureCredential());

            }
            catch (Exception ex)
            {
                // Don't break loading if something goes wrong connecting to secrets vault
                logger.LogError(ex,
                    "Failed to connect to Key Vault {KeyVaultName} {Message}",
                    keyVaultName, ex.Message);
            }
        }
    }
}


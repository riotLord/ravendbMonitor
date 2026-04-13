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


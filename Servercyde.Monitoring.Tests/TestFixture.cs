using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servercyde.Monitoring.Core;
using Servercyde.Monitoring.Core.Email;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests.Fakes;

namespace Servercyde.Monitoring.Tests;

public abstract class TestFixture
{
    public static MonitorConfig Config => new()
    {
        ToEmail = "systemadmin@example.com",
        FromEmail = "servercyde-no-reply@example.com",
        EmailSubject = "Servercyde Operations Summary"
    };

    public static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string CertificateDirectoryPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    public TestServiceProvider Services { get; init; }
    public FakeEmailClient EmailClient { get; init; }
    public FakeHttpMessageHandler HttpInterceptor;

    protected TestFixture()
    {
        Services = [];
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddJsonFile("appsettings.test.json", optional:true);
        if (!ShouldLoadLiveAzureResources())
        {
            Environment.SetEnvironmentVariable("AzureKeyVault", null);
        }
        if (ShouldLoadUserSecrets())
        {
            configurationBuilder.AddUserSecrets<TestFixture>(optional: true);
        }
        var logger = LoggerFactory
            .Create(x=>x.AddConsole()
            .SetMinimumLevel(LogLevel.Information))
            .CreateLogger("TestFixture");

        Services.AddHttpInterceptor(out HttpInterceptor);
        Bootstrapper.ConfigureServices(logger, Services, configurationBuilder, HttpInterceptor);

        EmailClient = new FakeEmailClient();
        Services.AddSingleton<IEmailClient>(EmailClient);
        Services.AddSingleton<IEmailService>(new EmailService(EmailClient));
    }

    private static bool ShouldLoadUserSecrets()
        => string.Equals(
            Environment.GetEnvironmentVariable("ENABLE_TEST_USER_SECRETS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static bool ShouldLoadLiveAzureResources()
        => string.Equals(
            Environment.GetEnvironmentVariable("ENABLE_TEST_LIVE_AZURE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}



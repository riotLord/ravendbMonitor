using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests;
using Servercyde.Monitoring.Tests.TestAttributes;

namespace Servercyde.Monitoring.Core.Tests.Database;

[IntegrationTest]
public class IntegrationTests
{
    [Fact]
    public async Task Can_get_summaries_from_raven_dev()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ENABLE_TEST_LIVE_AZURE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // We are not using a function host, so we do not have access to the Environment variables
        // we need to set the variable here first to mimic the integration so the bootstrapper
        // works as intended.
        Environment.SetEnvironmentVariable("AzureKeyVault", "Servercyde-Pulse-Developer");

        TestServiceProvider Services = [];
        var logger = LoggerFactory
       .Create(x => x
       .AddConsole()
       .SetMinimumLevel(LogLevel.Debug))
       .CreateLogger("IntegrationTest");

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets<IntegrationTests>(true);
        configurationBuilder.AddEnvironmentVariables();
        Bootstrapper.ConfigureServices(logger, Services, configurationBuilder);

        var sut = Services.GetRequiredService<RavenDbMonitor>();
        var summaries = await sut.GetSummaries("default", CancellationToken.None);
        summaries.Should().NotBeEmpty();
        Console.WriteLine(JsonConvert.SerializeObject(summaries, Formatting.Indented));
    }
}



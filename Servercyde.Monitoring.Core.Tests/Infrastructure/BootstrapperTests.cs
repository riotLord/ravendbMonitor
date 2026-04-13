using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests.TestAttributes;

namespace Servercyde.Monitoring.Core.Tests.Infrastructure;

public class BootstrapperTests
{
    [Fact]
    public void Bootstrapper_can_attempt_KeyVault_config()
    {
        var logger = LoggerFactory
            .Create(x => x
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger("Test");

        var configBuilder = new ConfigurationBuilder();

        Bootstrapper.AddKeyVaultConfiguration(logger, configBuilder, "SomeKeyVault");
        var act = configBuilder.Build;
        act.Should().Throw<Azure.Identity.CredentialUnavailableException>();
    }

    [Fact]
    [IntegrationTest]
    public void Bootstrapper_can_load_KeyVault_config()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ENABLE_TEST_LIVE_AZURE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var logger = LoggerFactory
            .Create(x => x
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger("Test");

        var configBuilder = new ConfigurationBuilder();

        Bootstrapper.AddKeyVaultConfiguration(logger, configBuilder, "Servercyde-Pulse-Developer");
        var act = configBuilder.Build;
        act.Should().NotThrow();
    }
}



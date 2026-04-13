using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Servercyde.Monitoring.Core.Email;
using Servercyde.Monitoring.Tests;
using Servercyde.Monitoring.Tests.Fakes;

namespace Servercyde.Monitoring.Core.Tests.Email;

public class EmailServiceTests : TestFixture
{
    [Fact]
    public async Task EmailService_SendEmailAsync_with_valid_input_sends_email()
    {
        var content = "<h1>HTML Content </h1>";
        var from = "test-from@example.com";
        var to = "test-to@example.com";
        var subject = "Hello World";

        var emailService = Services.GetRequiredService<IEmailService>();
        (await emailService.SendEmailAsync(new(subject, from, to, content), TestContext.Current.CancellationToken))
            .Status
            .Should().Be("Succeeded");

        EmailClient.Messages.Should().BeEquivalentTo([
            new FakeEmailClient.Message(
                Recipient: to,
                Subject: subject,
                Contents: content,
                Sender: from
            )
        ]);
    }

    [Fact]
    public void Can_provide_AzureCommunicationServices_config()
    {
        var connectionString = "endpoint=https://example.communication.azure.com/;accesskey=SomeKey";
        Services.PostConfigure<AzureCommunicationServicesConfig>(x => x.ConnectionString = connectionString);

        Services.GetRequiredService<IOptions<AzureCommunicationServicesConfig>>()
            .Value.ConnectionString
            .Should().Be(connectionString);
    }
}

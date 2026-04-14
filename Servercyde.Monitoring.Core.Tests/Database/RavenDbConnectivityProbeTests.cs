using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Tests;
using System.Net;

namespace Servercyde.Monitoring.Core.Tests.Database;

public class RavenDbConnectivityProbeTests : TestFixture
{
    [Fact]
    public async Task Probe_Should_Return_Success_When_RavenDb_RespondsWithOk()
    {
        HttpInterceptor.RespondWithOK("""{"Databases": []}""");
        var probe = Services.GetRequiredService<IRavenDbConnectivityProbe>();

        var result = await probe.Probe(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ConfigurationLoaded.Should().BeTrue();
        result.TlsRequestSucceeded.Should().BeTrue();
        result.HttpStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Probe_Should_Return_Failure_When_RavenDb_Returns_NonSuccess_Status()
    {
        HttpInterceptor.AddHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var probe = Services.GetRequiredService<IRavenDbConnectivityProbe>();

        var result = await probe.Probe(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TlsRequestSucceeded.Should().BeFalse();
        result.HttpStatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        result.FailureStage.Should().Be("HttpRequest");
        result.ExceptionMessage.Should().Be("RavenDB probe returned HTTP 401.");
    }

    [Fact]
    public async Task Probe_Should_Return_Sanitized_Exception_Details_When_Handler_Throws()
    {
        HttpInterceptor.AddHandler(_ => throw new HttpRequestException("client certificate rejected"));
        var probe = Services.GetRequiredService<IRavenDbConnectivityProbe>();

        var result = await probe.Probe(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TlsRequestSucceeded.Should().BeFalse();
        result.FailureStage.Should().Be("TlsHandshake");
        result.ExceptionType.Should().Be(nameof(HttpRequestException));
        result.ExceptionMessage.Should().Be("client certificate rejected");
    }
}

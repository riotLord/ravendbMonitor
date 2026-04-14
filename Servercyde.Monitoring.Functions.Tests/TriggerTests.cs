using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Servercyde.Monitoring.Core;
using Servercyde.Monitoring.Core.Database;
using System.Collections.Specialized;
using System.Security.Authentication;
using Telerik.JustMock;

namespace Servercyde.Monitoring.Functions.Tests;

public class TriggersTests
{
    [Fact]
    public async Task HttpGetTrigger_ValidProfile_ReturnsOkResult()
    {
        var mockReporter = Mock.Create<IReporter>();
        var mockProbe = Mock.Create<IRavenDbConnectivityProbe>();
        var triggers = new Triggers(mockReporter, mockProbe);

        var mockHttpRequestData = Mock.Create<HttpRequestData>();
        Mock.Arrange(() => mockHttpRequestData.Query)
            .Returns(new NameValueCollection
            {
                { "profile", "testProfile" }
            });

        Mock.Arrange(() => mockReporter.SendReport("testProfile", default))
            .Returns(Task.CompletedTask);

        IActionResult httpResult = await triggers.HttpGetTrigger(mockHttpRequestData);

        httpResult.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)httpResult).Value.Should().Be("Ok");
        Mock.Assert(() => mockReporter.SendReport("testProfile", default), Occurs.Once());
    }

    [Fact]
    public async Task HttpGetTrigger_ExceptionThrown_ReturnsInternalServerError()
    {
        var mockReporter = Mock.Create<IReporter>();
        var mockProbe = Mock.Create<IRavenDbConnectivityProbe>();
        Mock.Arrange(() => mockReporter.SendReport(Arg.AnyString, default))
            .Throws(new Exception("Test exception"));

        var triggers = new Triggers(mockReporter, mockProbe);

        var mockHttpRequestData = Mock.Create<HttpRequestData>();
        Mock.Arrange(() => mockHttpRequestData.Query)
            .Returns(new NameValueCollection
            {
                { "profile", "simulate" }
            });

        IActionResult httpResult = await triggers.HttpGetTrigger(mockHttpRequestData);

        httpResult.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)httpResult;
        objectResult.StatusCode.Should().Be(500);
        ((string)objectResult.Value!).Should().StartWith("Error: Test exception");

        Mock.Assert(() => mockReporter.SendReport("simulate", default), Occurs.Once());
    }

    [Fact]
    public async Task CheckRavenDbConnectionHttp_When_Probe_Succeeds_ReturnsOkResult()
    {
        var mockReporter = Mock.Create<IReporter>();
        var mockProbe = Mock.Create<IRavenDbConnectivityProbe>();
        var probeResult = new RavenDbConnectivityProbeResult(
            ConfigurationLoaded: true,
            CertificateConfigured: true,
            CertificateLoaded: true,
            PrivateKeyPresent: true,
            TlsRequestSucceeded: true,
            HttpStatusCode: 200,
            RavenDbUrl: "https://serverone.example.com",
            CertificateThumbprint: "thumb",
            CertificateSubject: "CN=Servercyde",
            CertificateExpiresUtc: DateTime.UtcNow.AddDays(30),
            CertificateLoadStrategy: "EphemeralKeySet",
            CertificateSource: "Base64",
            FailureStage: null,
            ExceptionType: null,
            ExceptionMessage: null,
            InnerExceptionType: null,
            InnerExceptionMessage: null);

        Mock.Arrange(() => mockProbe.Probe(default))
            .Returns(Task.FromResult(probeResult));

        var triggers = new Triggers(mockReporter, mockProbe);
        var mockHttpRequestData = Mock.Create<HttpRequestData>();

        IActionResult httpResult = await triggers.CheckRavenDbConnectionHttp(mockHttpRequestData);

        httpResult.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)httpResult).Value.Should().BeEquivalentTo(probeResult);
    }

    [Fact]
    public async Task CheckRavenDbConnectionHttp_When_Probe_Fails_ReturnsInternalServerError()
    {
        var mockReporter = Mock.Create<IReporter>();
        var mockProbe = Mock.Create<IRavenDbConnectivityProbe>();
        var probeResult = new RavenDbConnectivityProbeResult(
            ConfigurationLoaded: true,
            CertificateConfigured: true,
            CertificateLoaded: true,
            PrivateKeyPresent: true,
            TlsRequestSucceeded: false,
            HttpStatusCode: null,
            RavenDbUrl: "https://serverone.example.com",
            CertificateThumbprint: "thumb",
            CertificateSubject: "CN=Servercyde",
            CertificateExpiresUtc: DateTime.UtcNow.AddDays(30),
            CertificateLoadStrategy: "EphemeralKeySet",
            CertificateSource: "Base64",
            FailureStage: "TlsHandshake",
            ExceptionType: "HttpRequestException",
            ExceptionMessage: "client certificate rejected",
            InnerExceptionType: nameof(AuthenticationException),
            InnerExceptionMessage: "The remote certificate is invalid according to the validation procedure.");

        Mock.Arrange(() => mockProbe.Probe(default))
            .Returns(Task.FromResult(probeResult));

        var triggers = new Triggers(mockReporter, mockProbe);
        var mockHttpRequestData = Mock.Create<HttpRequestData>();

        IActionResult httpResult = await triggers.CheckRavenDbConnectionHttp(mockHttpRequestData);

        httpResult.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)httpResult;
        objectResult.StatusCode.Should().Be(500);
        objectResult.Value.Should().BeEquivalentTo(probeResult);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Servercyde.Monitoring.Core;
using Telerik.JustMock;
using FluentAssertions;
using System.Collections.Specialized;

namespace Servercyde.Monitoring.Functions.Tests;

public class TriggersTests
{
    
    [Fact]
    public async Task HttpGetTrigger_ValidProfile_ReturnsOkResult()
    {
        var mockReporter = Mock.Create<IReporter>();
        var triggers = new Triggers(mockReporter);

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
        Mock.Arrange(() => mockReporter.SendReport(Arg.AnyString, default))
            .Throws(new Exception("Test exception"));
       
        var triggers = new Triggers(mockReporter);

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
        ((string)objectResult?.Value!).Should().StartWith("Error: Test exception");

        Mock.Assert(() => mockReporter.SendReport("simulate", default), Occurs.Once());
    }
}

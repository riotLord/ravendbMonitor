using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Servercyde.Monitoring.Core;
using Servercyde.Monitoring.Core.Database;

namespace Servercyde.Monitoring.Functions;

public class Triggers(
    IReporter reporter,
    IRavenDbConnectivityProbe ravenDbConnectivityProbe)
{
    [Function("CheckRavenDbAlertsHttp")]
    public async Task<IActionResult> HttpGetTrigger(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req
    )
    {
        try
        {
            await reporter.SendReport(req.Query["profile"]);
            return new OkObjectResult("Ok");
        }
        catch (Exception ex)
        {
            return new ObjectResult($"Error: {ex.Message}") { StatusCode = 500 };
        }
    }

    [Function("CheckRavenDbConnectionHttp")]
    public async Task<IActionResult> CheckRavenDbConnectionHttp(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req
    )
    {
        var result = await ravenDbConnectivityProbe.Probe();
        return result.Success
            ? new OkObjectResult(result)
            : new ObjectResult(result) { StatusCode = 500 };
    }
}

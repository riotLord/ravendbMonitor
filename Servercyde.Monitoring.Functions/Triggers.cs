using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Servercyde.Monitoring.Core;

namespace Servercyde.Monitoring.Functions;

public class Triggers(IReporter reporter)
{
    [Function("CheckRavenDbAlertsHttp")]
    public async Task<IActionResult> HttpGetTrigger(
        [HttpTrigger(AuthorizationLevel.Function, "get" )] HttpRequestData req
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
}

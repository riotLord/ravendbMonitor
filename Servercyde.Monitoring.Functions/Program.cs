using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servercyde.Monitoring.Core.Infrastructure;

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.

var logger = LoggerFactory
    .Create(c => c
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information))
    .CreateLogger("Program");

logger.LogInformation("Monitor starting configuration...");

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Configuration.AddUserSecrets<Program>(true);
Bootstrapper.ConfigureServices(logger, builder.Services, builder.Configuration);
await builder.Build().RunAsync();


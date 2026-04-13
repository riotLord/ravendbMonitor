using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Core.Infrastructure;
using Servercyde.Monitoring.Tests;
using Xunit.Internal;

namespace Servercyde.Monitoring.Core.Tests.Database;

public class RavenDbMonitorTests : TestFixture
{
    [Fact]
    public async Task GetSummaries_Should_Return_DatabaseList()
    {
        var firstServerName = "https://serverone.example.com";
        var secondServerName = "https://servertwo.example.com";

        Services.AddSingleton(Options.Create(new RavenConfig()
        {
            Urls = [firstServerName, secondServerName]
        }));

        HttpInterceptor.RespondWithOK(
            """
            {
                "Databases": [
                    {
                        "Name": "TestDB1",
                        "AlertsCount": 2,
                        "PerformanceHintsCount": 1,
                        "Id": "TestDB1",
                        "DocumentsCount": 1000,
                        "IndexesCount": 5
                    },
                    {
                        "Name": "TestDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB2",
                        "DocumentsCount": 500,
                        "IndexesCount": 3                            
                    }
                ]                      
            }    
              
            """);


        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        (await monitor.GetSummaries("default", CancellationToken.None))
        .Should().BeEquivalentTo([
            new DatabaseSummary("https://serverone.example.com", "TestDB1", 2, 1, 1000, 5),
            new DatabaseSummary("https://serverone.example.com", "TestDB2", 0, 0, 500, 3),
            new DatabaseSummary("https://servertwo.example.com", "TestDB1", 2, 1, 1000, 5),
            new DatabaseSummary("https://servertwo.example.com", "TestDB2", 0, 0, 500, 3)
        ]);
    }

    public static TheoryData<string[], string[]> DatabaseExcludes => new()
    {
        {
            ["*B1"],
            ["TestDB2", "ExampleDB2", "shared-db-prod", "shared-db-dev", "shared-db-test", "shared-db-example"]
        },
        {
            ["*example*","*test*"],
            ["shared-db-prod", "shared-db-dev"]
        },
        {
            ["*DB2"],
            ["TestDB1", "ExampleDB1", "shared-db-prod", "shared-db-dev", "shared-db-test", "shared-db-example"]
        },
        {
            ["*o*"],
            ["TestDB1", "ExampleDB1", "TestDB2", "ExampleDB2", "shared-db-dev", "shared-db-test", "shared-db-example"]
        },
        {
            ["*"],
            []
        }
    };
  
    [Theory]
    [MemberData(nameof(DatabaseExcludes))]
    public async Task GetSummaries_Should_Not_Return_Excluded_Databases(string[] filter, string[] summaryNames)
    {

        DatabaseSummary[] expectedSummaries = summaryNames switch
        {
            { Length: 0 } => [],
            _ => [.. summaryNames.Select(x => new DatabaseSummary("https://serverone.example.com", x, 0, 0, 0, 0))]
        };            

        RavenConfig filteredConfig = new()
        {
            Urls = ["https://serverone.example.com"],
            DatabaseExcludes = filter
        };

        Services.AddSingleton(Options.Create(filteredConfig));
        HttpInterceptor.RespondWithOK(
            """
            {
                "Databases": [
                    {
                        "Name": "TestDB1",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB1",
                        "DocumentsCount": 0,
                        "IndexesCount": 0
                    },
                    {
                        "Name": "TestDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB2",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "ExampleDB1",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "ExampleDB1",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "ExampleDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "ExampleDB2",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-prod",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-prod",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-test",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-test",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-dev",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-dev",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-example",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-example",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    }
                ]                      
            }    
              
            """);

        var monitor = Services.GetRequiredService<RavenDbMonitor>();

        (await monitor.GetSummaries("default", CancellationToken.None))
        .Should()
        .BeEquivalentTo(expectedSummaries);
    }

    public static TheoryData<string[], string[]> DatabaseIncludes => new()
    {
        {
            ["*-db-*", "*shared*"],
            ["shared-db-prod", "shared-db-dev", "shared-db-test", "shared-db-example", "shared-db-best", "shared-db-rest"]
        },
        {
            ["*est"],
            ["shared-db-test", "shared-db-best", "shared-db-rest"]
        },
        {
            ["e*x*", "*db-e*"], // if second case was not set shared-db-example will not show,
            ["ExampleDB1", "ExampleDB2", "shared-db-example"]
        },
        {
            ["tE*"],
            ["TestDB1", "TestDB2"]
        },
        {
            ["q"],
            []
        }
    };

    [Theory]
    [MemberData(nameof(DatabaseIncludes))]
    public async Task GetSummaries_Should_Return_Included_Databases(string[] filter, string[] summaryNames)
    {
        DatabaseSummary[] expectedSummaries = summaryNames switch
        {
            { Length: 0 } => [],
            _ => [.. summaryNames.Select(x => new DatabaseSummary("https://serverone.example.com", x, 0, 0, 0, 0))]
        };

        RavenConfig filteredConfig = new()
        {
            Urls = ["https://serverone.example.com"],
            DatabaseIncludes = filter
        };

        Services.AddSingleton(Options.Create(filteredConfig));
        HttpInterceptor.RespondWithOK(
            """
            {
                "Databases": [
                    {
                        "Name": "TestDB1",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB1",
                        "DocumentsCount": 0,
                        "IndexesCount": 0
                    },
                    {
                        "Name": "TestDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB2",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "ExampleDB1",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "ExampleDB1",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "ExampleDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "ExampleDB2",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-prod",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-prod",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-test",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-test",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },{
                        "Name": "shared-db-best",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-best",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-rest",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-rest",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-dev",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-dev",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    },
                    {
                        "Name": "shared-db-example",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "shared-db-example",
                        "DocumentsCount": 0,
                        "IndexesCount": 0                            
                    }
                ]                      
            }    
              
            """);

        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        var result = await monitor.GetSummaries("default", CancellationToken.None);
        (await monitor.GetSummaries("default", CancellationToken.None))
        .Should().BeEquivalentTo(expectedSummaries);
    }

    [Fact]
    public async Task GetSummaries_Should_Return_Databases_from_filter_with_includes_and_excludes()
    {
        RavenConfig filteredConfig = new()
        {
            Urls = ["https://serverone.example.com"],
            DatabaseIncludes = ["*example*"],
            DatabaseExcludes = ["*shared*"]
        };
        Services.AddSingleton(Options.Create(filteredConfig));
        HttpInterceptor.RespondWithOK(
            """
            {
                "Databases": [
                    {
                        "Name": "TestDB1",
                        "AlertsCount": 2,
                        "PerformanceHintsCount": 1,
                        "Id": "TestDB1",
                        "DocumentsCount": 1000,
                        "IndexesCount": 5
                    },
                    {
                        "Name": "TestDB2",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "TestDB2",
                        "DocumentsCount": 500,
                        "IndexesCount": 3                            
                    },
                    {
                        "Name": "ExampleDB1",
                        "AlertsCount": 0,
                        "PerformanceHintsCount": 0,
                        "Id": "ExampleDB1",
                        "DocumentsCount": 100,
                        "IndexesCount": 7                            
                    },
                    {
                        "Name": "ExampleDB2",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "ExampleDB2",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-rest",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-rest",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-best",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-best",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-prod",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-prod",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-test",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-test",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-dev",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-dev",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    },
                    {
                        "Name": "shared-db-example",
                        "AlertsCount": 1,
                        "PerformanceHintsCount": 1,
                        "Id": "shared-db-example",
                        "DocumentsCount": 300,
                        "IndexesCount": 8                            
                    }
                ]                       
            }    
              
            """);

        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        (await monitor.GetSummaries("default", CancellationToken.None))
        .Should().BeEquivalentTo([
                new DatabaseSummary("https://serverone.example.com", "ExampleDB1", 0, 0, 100, 7),
                new DatabaseSummary("https://serverone.example.com", "ExampleDB2", 1, 1, 300, 8),
            ]);
    }

    [Fact]
    public async Task GetSummaries_Should_Return_DatabaseAlerts()
    {
        HttpInterceptor.AddHandlers(
            (
            "/databases/TestDB1/queries", 
            """
            {
                "Results": [
                    {
                        "@metadata": {
                            "@id": "QueuedCommands/1"
                        },
                        "Id": "QueuedCommands/1",
                        "Type": "EmailNotification",
                        "RetriesRemaining": 0
                    },
                    {
                        "@metadata": {
                            "@id": "QueuedCommands/2"
                        },
                        "Id": "QueuedCommands/2",
                        "Type": "DataSynchronization",
                        "RetriesRemaining": 0
                    }
                ]
            }
            """),

            ("TestDB1/notifications", """
            {
                "Results" : [
                    {
                        "Database": "TestDB1",
                        "Id": "alert/1",
                        "Title": "High CPU Usage",
                        "Message": "Database CPU usage is above 80%",
                        "CreatedAt": "2025-03-05T12:34:56.789Z",
                        "Severity": "Warning",
                        "Category": "Performance"
                    },
                    {
                        "Database": "TestDB1",
                        "Id": "alert/2",
                        "Title": "Index Stalled",
                        "Message": "Index 'Users/ByEmail' is stalled",
                        "CreatedAt": "2025-03-05T11:22:33.444Z",
                        "Severity": "Error",
                        "Category": "Indexing"
                    }
                ]
            }
            """),

            ("/databases", """
            {
                "Databases": [
                 {
                    "Name": "TestDB1",
                    "AlertsCount": 2,
                    "PerformanceHintsCount": 1,
                    "Id": "TestDB1",
                    "DocumentsCount": 1000,
                    "IndexesCount": 5   
                 },
                {
                    "Name": "TestDB2",
                    "AlertsCount": 0,
                    "PerformanceHintsCount": 0,
                    "Id": "TestDB2",
                    "DocumentsCount": 500,
                    "IndexesCount": 3         
                }
                ]
            }
            """));

        var firstServerName = "https://serverone.example.com/studio/index.html#databases/documents?&database=TestDB1";
        var secondServerName = "https://servertwo.example.com/studio/index.html#databases/documents?&database=TestDB1";

        Services.AddSingleton(Options.Create(new RavenConfig()
        {
            Urls = ["https://serverone.example.com/", "https://servertwo.example.com/"]
        }));

        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        var result = await monitor.GetSummaries("default", CancellationToken.None);
 
        (await monitor.GetSummaries("default", CancellationToken.None))
        .SelectMany(x => x.Alerts)
            .Should()
            .BeEquivalentTo([
                new DatabaseAlert(firstServerName, "TestDB1", "alert/1", "High CPU Usage",
                    "Database CPU usage is above 80%", DateTime.UtcNow,
                    "Performance", DatabaseAlert.SeverityLevel.Warning),
                new DatabaseAlert(firstServerName, "TestDB1", "alert/2", "Index Stalled",
                    "Index 'Users/ByEmail' is stalled", DateTime.UtcNow,
                    "Indexing", DatabaseAlert.SeverityLevel.Error),
                new DatabaseAlert(firstServerName, "TestDB1", "QueuedCommands/1", "QueuedCommand Retries = 0",
                    "A QueuedCommand has run out of retries.", DateTime.UtcNow,
                    "RetryFailure", DatabaseAlert.SeverityLevel.Critical),
                new DatabaseAlert(firstServerName, "TestDB1", "QueuedCommands/2", "QueuedCommand Retries = 0",
                    "A QueuedCommand has run out of retries.", DateTime.UtcNow,
                    "RetryFailure", DatabaseAlert.SeverityLevel.Critical),
                new DatabaseAlert(secondServerName, "TestDB1", "alert/1", "High CPU Usage",
                    "Database CPU usage is above 80%", DateTime.UtcNow,
                    "Performance", DatabaseAlert.SeverityLevel.Warning),
                new DatabaseAlert(secondServerName, "TestDB1", "alert/2", "Index Stalled",
                    "Index 'Users/ByEmail' is stalled", DateTime.UtcNow,
                    "Indexing", DatabaseAlert.SeverityLevel.Error),
                new DatabaseAlert(secondServerName, "TestDB1", "QueuedCommands/1", "QueuedCommand Retries = 0",
                    "A QueuedCommand has run out of retries.", DateTime.UtcNow,
                    "RetryFailure", DatabaseAlert.SeverityLevel.Critical),
                new DatabaseAlert(secondServerName, "TestDB1", "QueuedCommands/2", "QueuedCommand Retries = 0",
                    "A QueuedCommand has run out of retries.", DateTime.UtcNow,
                    "RetryFailure", DatabaseAlert.SeverityLevel.Critical)
                ], options => options.Excluding(x => x.CreatedAt));
    }



    [Fact]
    public async Task GetSummaries_Should_Parse_Json_Without_Error()
    {
        DatabaseAlert[] expectedDatabaseAlerts = [];
        HttpInterceptor.RespondWithOK(
            """
            {
                "Databases": [
                {
                    "Name": "TestDB1",
                    "AlertsCount": 2,
                    "PerformanceHintsCount": 1,
                    "Id": "TestDB1"             
                },
                {
                    "Name": "TestDB2",
                    "AlertsCount": 0,
                    "PerformanceHintsCount": 0,
                    "Id": "TestDB2"          
                }]                      
            }    
            """);

        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        (await monitor.GetSummaries("default", CancellationToken.None))
            .SelectMany(x => x.Alerts)
            .Should()
            .BeEquivalentTo(expectedDatabaseAlerts);
    }

    [Fact]
    public async Task GetSummaries_Should_Return_Simulated_Alerts()
    {
        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        (await monitor.GetSummaries("simulate", CancellationToken.None))
        .SelectMany(x => x.Alerts)
          .Should()
          .Contain(x =>
            FakeDatabaseAlertFactory.AllAlertTitles.Contains(x.Title) &&
            FakeDatabaseAlertFactory.AllAlertCategories.Contains(x.Category) &&
            x.Message.Contains("Simulated alert") &&
            DatabaseAlert.AllSeverities.Contains(x.Severity));
    }
    

    [Fact]
    public async Task GetQueuedCommandsWithZeroRetriesRemaining_Should_Return_Alerts()
    {
        HttpInterceptor.RespondWithOK(
            """
            {
                "Results": [
                    {
                        "@metadata": {
                            "@id": "QueuedCommands/1"
                        },
                        "Id": "QueuedCommands/1",
                        "Type": "EmailNotification",
                        "RetriesRemaining": 0
                    },
                    {
                        "@metadata": {
                            "@id": "QueuedCommands/2"
                        },
                        "Id": "QueuedCommands/2",
                        "Type": "DataSynchronization",
                        "RetriesRemaining": 0
                    }
                ]
            }
            """);

        var monitor = Services.GetRequiredService<RavenDbMonitor>();
        var server = "https://serverone.example.com";
        DatabaseAlert[] ExpectedAlerts =
        [ 
            new(server, "TestDB1", "QueuedCommands/1", "QueuedCommand Retries = 0",
                "A QueuedCommand has run out of retries.", DateTime.MinValue,
                "RetryFailure", DatabaseAlert.SeverityLevel.Critical),
            new(server, "TestDB1", "QueuedCommands/2", "QueuedCommand Retries = 0",
                "A QueuedCommand has run out of retries.", DateTime.MinValue,
                "RetryFailure", DatabaseAlert.SeverityLevel.Critical)
        ];

        (await monitor.GetQueuedCommandsWithZeroRetriesRemaining(server, "TestDB1", CancellationToken.None))
        .Should()
        .BeEquivalentTo(ExpectedAlerts,
            options =>
                options.Excluding(x => x.CreatedAt));
    }

    [Fact]
    public void RavenDbMonitor_Should_Throw_InvalidOperationException_When_Certificate_Not_Found()
    {
        var act = () => Services.AddRavenDb(new()
        {
            Urls = ["https://serverone.example.com"],
            CertificateThumbprint = "nonexistentthumbprint"
        });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Certificate with thumbprint nonexistentthumbprint was not found");
    }

    [Fact]
    public void RavenDbMonitor_Should_LoadCertificate_When_CertificateDirectoryPath_Is_Provided()
    {
        CertificateLoader.SetCertificateDirectoryPath(CertificateDirectoryPath);
        var act = () => Services.AddRavenDb(new() {
            Urls = ["https://serverone.example.com"],
            CertificateThumbprint = "883D8A563352A56B56ABF340C2926B6D7217605D",
        });
        act.Should().NotThrow();
        Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("RavenDB");
    }

    [Fact]
    public void RavenDbMonitor_Should_LoadCertificate_When_CertificateBase64_Is_Provided()
    {
        var certificatePath = Path.Combine(CertificateDirectoryPath, "883D8A563352A56B56ABF340C2926B6D7217605D.p12");
        var certificateBase64 = Convert.ToBase64String(File.ReadAllBytes(certificatePath));

        var act = () => Services.AddRavenDb(new()
        {
            Urls = ["https://serverone.example.com"],
            CertificateBase64 = certificateBase64
        });

        act.Should().NotThrow();
        Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("RavenDB");
    }

    [Fact]
    public void AddRavenDb_should_throw_when_no_URLS_are_configured()
    {
        var act = () => Services.AddRavenDb(new());
        act.Should().Throw<ArgumentNullException>();
    }



}


#region usings
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Core.Email;
using Servercyde.Monitoring.Tests;
using Servercyde.Monitoring.Tests.Fakes;
using Telerik.JustMock;
#endregion

namespace Servercyde.Monitoring.Core.Tests;

public class ReporterTests : TestFixture
{
    private readonly IMonitor _mockMonitor;
    private readonly FakeLogger<Reporter> _fakeLogger = new();

    public ReporterTests()
    {
        _mockMonitor = Mock.Create<IMonitor>();

        Services.AddSingleton(Options.Create(new RavenConfig()
        {
            Urls = ["http://localhost:8080"]
        }));
        Services.AddSingleton(_mockMonitor);
        Services.AddSingleton<ILogger<IReporter>>(_fakeLogger);
        Services.AddSingleton<IReporter, Reporter>();
    }

    #region private methods
    private void ArrangeMonitorGetDatabaseSummaries(DatabaseSummary[] summaries,
        string profile = "default") =>
    Mock.Arrange(() => _mockMonitor
        .GetSummaries(profile, Arg.IsAny<CancellationToken>()))
        .ReturnsAsync(summaries)
        .OccursOnce();

    #endregion

    [Fact]
    public async Task SendReport_When_No_Alerts_Should_Send_AllClear_Email_With_Metrics()
    {
        ArrangeMonitorGetDatabaseSummaries([new("serverone.example.com","TestDB1", 0, 0, 100, 5),
        new("serverone.example.com", "TestDB2", 0, 0, 200, 3)]);
       
        var notifier = Services.GetRequiredService<IReporter>();

        await notifier.SendReport(cancellationToken: CancellationToken);

        var message = EmailClient.Messages.First();
        message.Subject.Should().Be($"{Config.EmailSubject}: All Clear");
        message.Recipient.Should().Be(Config.ToEmail);
        message.Contents.Should().Contain("TestDB1");
        message.Contents.Should().Contain("TestDB2");
        message.Contents.Should().Contain("Indexes");
        message.Contents.Should().Contain("There were not any alerts detected.");
    }

    [Fact]
    public async Task SendReport_With_Alerts_Should_Send_AlertsDetected_Email()
    {
        #region arrange monitor get database summaries
        ArrangeMonitorGetDatabaseSummaries([
            new("serverone.example.com","TestDB1", 2, 1, 100, 5) {
                Alerts = [
                 new("serverone.example.com",
                    "TestDB1",
                    "alert/1",
                    "High CPU Usage",
                    "Database CPU usage is above 80%",
                    DateTime.UtcNow,
                    "Performance",
                    DatabaseAlert.SeverityLevel.Warning
                ),
                new(
                    "serverone.example.com",
                    "TestDB1",
                    "alert/2",
                    "Index Stalled",
                    "Index 'Users/ByEmail' is stalled",
                    DateTime.UtcNow,
                    "Indexing",
                    DatabaseAlert.SeverityLevel.Error
                )
            ]},
            new("serverone.example.com", "TestDB2", 0, 0, 200, 3) {
                Alerts = []
            }
        ]);
        #endregion

        await Services.GetRequiredService<IReporter>().SendReport(cancellationToken: CancellationToken);
        var message = EmailClient.Messages.First();
        message.Subject.Should().Be($"{Config.EmailSubject}: Alerts Detected");
        message.Contents.Should().Contain("Index Stalled");
        message.Contents.Should().Contain("Database CPU usage is above 80%");
    }

    [Fact]
    public async Task SendReport_Should_not_send_email_with_alerts_detected_subject()
    {
        #region arrange monitor get database summaries
        ArrangeMonitorGetDatabaseSummaries([
            new ("serverone.example.com", "TestDB1", 1, 1, 100, 5)
            {
                Alerts = [
                    new ("serverone.example.com",
                        "TestDB1",
                        "alert1",
                        "Simulated Alert",
                        "A simulated alert for testing.",
                        DateTime.UtcNow,
                        "Simlation",
                        DatabaseAlert.SeverityLevel.Info)
                    ]
            }
            ]);
        #endregion

        await Services.GetRequiredService<IReporter>().SendReport(cancellationToken: CancellationToken);
        var message = EmailClient.Messages.First();
        message.Subject.Should().Be($"{Config.EmailSubject}");
    }

    [Fact]
    public async Task SendReport_With_Only_QueuedCommands_Should_Send_AlertsDetected_Email()
    {
        #region arrange monitor get database summaries
        DatabaseAlert[] queuedCommands =
        [
            new("serverone.example.com","TestDB1", "alert/1", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", DatabaseAlert.SeverityLevel.Critical),
            new("serverone.example.com", "TestDB1", "alert/2", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", DatabaseAlert.SeverityLevel.Critical)
        ];

        ArrangeMonitorGetDatabaseSummaries([
                new("serverone.example.com", "TestDB1", 2, 1, 100, 5) {
                    Alerts = queuedCommands
                },
                new("serverone.example.com", "TestDB2", 0, 0, 200, 3) {
                    Alerts = []
                }
            ]);
        #endregion

        await Services.GetRequiredService<IReporter>().SendReport(cancellationToken: CancellationToken);
        var message = EmailClient.Messages.First();
        message.Subject.Should().Be($"{Config.EmailSubject}: Alerts Detected");
        message.Contents.Should().Contain("A QueuedCommand has run out of retries.");
    }

    [Fact]
    public async Task SendReport_Should_Show_server_source_link_for_alerts()
    {
        var severity = DatabaseAlert.SeverityLevel.Critical;

        #region arrange monitor get database summaries
        DatabaseAlert[] serverOneQueuedCommands =
        [
            new("serverone.example.com","TestDB1", "alert/1", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", severity),
            new("serverone.example.com", "TestDB1", "alert/2", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", severity)
        ];

        DatabaseAlert[] serverTwoQueuedCommands =
        [
            new("servertwo.example.com","TestDB1", "alert/1", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", severity),
            new("servertwo.example.com", "TestDB1", "alert/2", "QueuedCommand Retries = 0", "A QueuedCommand has run out of retries.",
                DateTime.UtcNow, "RetryFailure", severity)
        ];

        ArrangeMonitorGetDatabaseSummaries([
                new("serverone.example.com", "TestDB1", 2, 1, 100, 5) {
                    Alerts = serverOneQueuedCommands
                },
                new("servertwo.example.com", "TestDB2", 0, 0, 200, 3) {
                    Alerts = serverTwoQueuedCommands
                }
            ]);
        #endregion

        await Services.GetRequiredService<IReporter>().SendReport(cancellationToken: CancellationToken);

        var serverBaseDomain = "serverone.example.com";

        var message = EmailClient.Messages.First();

        message.Subject.Should().Be($"{Config.EmailSubject}: Alerts Detected");
        message.Contents.Should().Contain($"<a href=\"{serverBaseDomain}\">serverone</a>");
    }

    [Fact]
    public async Task SendReport_With_Simulated_Profile_Should_Send_AlertsDetected_Email()
    {
        ArrangeMonitorGetDatabaseSummaries(FakeDatabaseSummariesFactory.Create(10), "simulate");

        var notifier = Services.GetRequiredService<IReporter>();
        await notifier.SendReport("simulate", CancellationToken);
        var message = EmailClient.Messages.First();
        message.Subject.Should().Be($"{Config.EmailSubject}: Alerts Detected");
        message.Recipient.Should().Be(Config.ToEmail);
        message.Contents.Should().Contain("Simulated alert");

    }

    [Fact]
    public async Task SendReport_With_Exception_Should_Log_Error()
    {
        Mock.Arrange(() => _mockMonitor.GetSummaries(Arg.AnyString, Arg.IsAny<CancellationToken>()))
            .Throws(new Exception("Test exception"));
        var notifier = Services.GetRequiredService<IReporter>();
        await notifier.SendReport(null, CancellationToken);
        _fakeLogger.LogEntries
            .Count(ex =>
                    ex.Message!
                    .Contains("Error occurred during RavenDB alert check"))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task SendReport_With_Exception_During_Monitor_And_Exception_Sending_ErrorEmail_Should_Log_Both_Errors()
    {
        var monitorException = new InvalidOperationException("Monitor connection failed!");
        var emailException = new Exception("Failed to connect to Azure Communication Services");

        Mock.Arrange(() => _mockMonitor.GetSummaries(Arg.AnyString, Arg.IsAny<CancellationToken>()))
            .Throws(monitorException);

        // Need to mock exception for this method so I could not use the TestFixture's EmailClient
        var mockEmailService = Mock.Create<IEmailService>();
        Services.AddSingleton(mockEmailService);
        Mock.Arrange(() => mockEmailService.SendEmailAsync(Arg.IsAny<MailMessage>(), Arg.IsAny<CancellationToken>()))
            .Throws(emailException);

        var reporter = Services.GetRequiredService<IReporter>();

        await reporter.SendReport(null, CancellationToken);

        _fakeLogger.LogEntries
            .Should().ContainSingle(log =>
                log.LogLevel == LogLevel.Error &&
                log.Exception == monitorException &&
                log.Message!.Contains("Error occurred during RavenDB alert check"));

        _fakeLogger.LogEntries
           .Should().ContainSingle(log =>
               log.LogLevel == LogLevel.Error &&
               log.Exception == emailException &&
               log.Message!.Contains("Failed to send the error notification email"));

        var message = EmailClient.Messages.Count().Should().Be(0);     

        _fakeLogger.LogEntries.Should().NotContain(log => log.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public async Task SSR1_SendReport_Metric_Should_Not_Have_Duplicate_Rows()
    {
        ArrangeMonitorGetDatabaseSummaries([
            new("serverone.example.com","TestDB1", 0, 0, 100, 5),
            new("servertwo.example.com","TestDB1", 0, 0, 100, 5),
            new("serverone.example.com", "TestDB2", 0, 0, 200, 3),
            new("servertwo.example.com","TestDB2", 0, 0, 100, 5),]);

        var notifier = Services.GetRequiredService<IReporter>();

        await notifier.SendReport(cancellationToken: CancellationToken);

        EmailClient
            .Messages.First()
            .Contents.Split("<tr>")
            .Count(x => x.Contains("TestDB1"))
            .Should()
            .Be(1);         
    }

    [Fact]
    public async Task SSR1_SendReport_Duplicate_Database_Alerts_Across_Multiple_Servers_Should_Be_Merged()
    {
        #region arrange monitor get database summaries
        ArrangeMonitorGetDatabaseSummaries([
            new("serverone.example.com","TestDB1", 2, 1, 100, 5) {
                Alerts = [
                 new("serverone.example.com",
                    "TestDB1",
                    "alert/1",
                    "High CPU Usage",
                    "Database CPU usage is above 80%",
                    DateTime.UtcNow,
                    "Performance",
                    DatabaseAlert.SeverityLevel.Warning
                ),
                new(
                    "serverone.example.com",
                    "TestDB1",
                    "alert/2",
                    "Index Stalled",
                    "Index 'Users/ByEmail' is stalled",
                    DateTime.UtcNow,
                    "Indexing",
                    DatabaseAlert.SeverityLevel.Error
                )
            ]},
            new("serverone.example.com", "TestDB2", 0, 0, 200, 3)  {
                Alerts = [
                 new("serverone.example.com",
                    "TestDB2",
                    "alert/1",
                    "High CPU Usage",
                    "Database CPU usage is above 80%",
                    DateTime.UtcNow,
                    "Performance",
                    DatabaseAlert.SeverityLevel.Warning
                ),
                new(
                    "serverone.example.com",
                    "TestDB2",
                    "alert/2",
                    "Index Stalled",
                    "Index 'Users/ByEmail' is stalled",
                    DateTime.UtcNow,
                    "Indexing",
                    DatabaseAlert.SeverityLevel.Error
                )
            ]},
            new("servertwo.example.com","TestDB1", 2, 1, 100, 5) {
                Alerts = [
                 new("servertwo.example.com",
                    "TestDB1",
                    "alert/1",
                    "High CPU Usage",
                    "Database CPU usage is above 80%",
                    DateTime.UtcNow,
                    "Performance",
                    DatabaseAlert.SeverityLevel.Warning
                ),
                new(
                    "servertwo.example.com",
                    "TestDB1",
                    "alert/2",
                    "Index Stalled",
                    "Index 'Users/ByEmail' is stalled",
                    DateTime.UtcNow,
                    "Indexing",
                    DatabaseAlert.SeverityLevel.Error
                )
            ]},
            new("servertwo.example.com", "TestDB2", 0, 0, 200, 3)  {
                Alerts = [
                 new("servertwo.example.com",
                    "TestDB2",
                    "alert/1",
                    "High CPU Usage",
                    "Database CPU usage is above 80%",
                    DateTime.UtcNow,
                    "Performance",
                    DatabaseAlert.SeverityLevel.Warning
                ),
                new(
                    "servertwo.example.com",
                    "TestDB2",
                    "alert/2",
                    "Index Stalled",
                    "Index 'Users/ByEmail' is stalled",
                    DateTime.UtcNow,
                    "Indexing",
                    DatabaseAlert.SeverityLevel.Error
                )
            ]}
        ]);
        #endregion

        var notifier = Services.GetRequiredService<IReporter>();

        await notifier.SendReport(cancellationToken: CancellationToken);

        EmailClient
        .Messages.First()
        .Contents.Split("<tr>")
        .Count(x => x.Contains("Database CPU usage is above 80%"))
        .Should()
        .Be(2);

        EmailClient
            .Messages.First()
            .Contents
            .Should()
            .Contain("<a href=\"servertwo.example.com\">servertwo</a> <a href=\"serverone.example.com\">serverone</a>");
    }
}

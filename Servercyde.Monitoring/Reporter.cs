using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servercyde.Monitoring.Core.Database;
using Servercyde.Monitoring.Core.Email;
using System.Linq;

namespace Servercyde.Monitoring.Core;
public interface IReporter
{
    Task SendReport(string? profileName = null, CancellationToken cancellationToken = default);
}
public class Reporter(
       IMonitor ravenDbMonitor,
       IEmailService emailService,
       ILogger<IReporter> logger = null!,
       IOptions<MonitorConfig> ravenDbNotifierConfig = null!
) : IReporter
{
    public const string DEFAULT_PROFILE_NAME = "default"; 

    public async Task SendReport(
        string? profileName = null, 
        CancellationToken cancellationToken = default
    ) 
    {
        var config = ravenDbNotifierConfig.Value;
        try
        {
            var allSummaries = await ravenDbMonitor.GetSummaries(profileName ?? DEFAULT_PROFILE_NAME, cancellationToken);
            
            MailMessage msg = BuildMailMessage(config, allSummaries);

            await emailService.SendEmailAsync(msg);
            
            logger.LogInformation("RavenDB Alert check completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during RavenDB alert check for profile: {Profile}", profileName ?? DEFAULT_PROFILE_NAME);

            await BuildAndSendExceptionEmail(config, ex);
        }
    }

    private async Task BuildAndSendExceptionEmail(MonitorConfig config, Exception ex)
    {
        try
        {
            var errorMsg = new MailMessage(
                $"{config.EmailSubject}: Error During Alert Check",
                config.FromEmail,
                config.ToEmail,
                BuildErrorEmailBody(ex)
            );

            await emailService.SendEmailAsync(errorMsg);

            logger.LogWarning("Sent error notification email to {Recipient} due to an exception during report generation.", config.ToEmail);
        }
        catch (Exception emailEx)
        {
            logger.LogError(emailEx, "Failed to send the error notification email after an initial error during RavenDB alert check.");
        }
    }

   private static MailMessage BuildMailMessage(
       MonitorConfig config,
       DatabaseSummary[] summaries
   )
    {
        var alerts = summaries.SelectMany(x => x.Alerts).ToArray();
        return alerts switch
        {
            [] => new MailMessage(
                $"{config.EmailSubject}: All Clear",
                config.FromEmail,
                config.ToEmail,
                BuildSummaryEmail(summaries)
            ),

            var x when x.Any(a => a.Severity >= DatabaseAlert.SeverityLevel.Warning) =>
                new MailMessage(
                    $"{config.EmailSubject}: Alerts Detected",
                    config.FromEmail,
                    config.ToEmail,
                    BuildSummaryEmail(summaries)),

            _ => new MailMessage(
                $"{config.EmailSubject}",
                    config.FromEmail,
                    config.ToEmail,
                    BuildSummaryEmail(summaries))
        };
    }


    private static string BuildErrorEmailBody(Exception ex)
    {
        var exceptionType = WebUtility.HtmlEncode(ex.GetType().FullName);
        var exceptionMessage = WebUtility.HtmlEncode(ex.Message);
        var stackTrace = WebUtility.HtmlEncode(ex.StackTrace);

        return $"""
            <h2>Error During RavenDB Alert Check</h2>
            <p>An unexpected error occurred while trying to generate the RavenDB alert report. Please investigate.</p>
            <hr>
            <p><strong>Error Type:</strong> {exceptionType}</p>
            <p><strong>Message:</strong> {exceptionMessage}</p>
            <p><strong>Stack Trace:</strong></p>
            <pre style='background-color: #f0f0f0; border: 1px solid #ccc; padding: 10px; overflow-x: auto;'>{stackTrace}</pre>
            """;
    }
    private static string BuildSummaryEmail(
        IEnumerable<DatabaseSummary> databaseSummaries)
    {
        var allAlerts = databaseSummaries.SelectMany(x => x.Alerts ?? []);

        // Sort alerts by Severity DESC, DB Name ASC, Time DESC
        var sortedAlerts = allAlerts
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Database)
            .ThenByDescending(x => x.CreatedAt);

        var alertRows = new StringBuilder();

        BuildAlertRows(sortedAlerts, alertRows, BuildSourceLinks(sortedAlerts));

        var dbMetricRows = new StringBuilder();

        foreach (var db in databaseSummaries
                .GroupBy(summary => summary.Name)
                .Select(group => group.First())
                .OrderBy(x => x.Name))
        {
            dbMetricRows.AppendLine($@"
                <tr>
                    <td style='border: 1px solid black;'>{db.Name}</td>
                    <td style='border: 1px solid black;'>{db.DocumentsCount:N0}</td>
                    <td style='border: 1px solid black;'>{db.IndexesCount:N0}</td>
                </tr>");
        }

        var emailSummaryContent = sortedAlerts.Count() switch
        {
            < 1 => "<p>There were not any alerts detected.</p>",
            > 0 => $"""
            <h2>RavenDB Alerts Summary</h2>
            <p>The following alerts have been detected:</p>
            
            <table border='1' cellpadding='5' style='border-collapse: collapse; text-align: left;'>
                <tr><th>Database</th><th>Severity</th><th>Source</th><th>Time</th><th>Category</th><th>Title</th><th>Message</th></tr>
                {alertRows}
            </table>
            """
        };

        var emailBody = $"""
           
            {emailSummaryContent}

            <h3>Database Metrics</h3>
            <table border='1' cellpadding='5' style='border-collapse: collapse; text-align: left;'>
                <tr><th>Database</th><th>Documents</th><th>Indexes</th></tr>
                {dbMetricRows}
            </table>
            """;


        return emailBody;
    }

    private static void BuildAlertRows(IOrderedEnumerable<DatabaseAlert> sortedAlerts, StringBuilder alertRows, List<(string Title, string Database, string Link)> links)
    {
        foreach (var alertGrp in from alertGroup in sortedAlerts
                    .GroupBy(alert => alert.Database)
                    .GroupBy(grp => grp.Key)
                    .OrderBy(x => x.Key)
                        let alerts = alertGroup.SelectMany(x => x)
                        from alertGrp in alerts.GroupBy(x => x.Title)
                        select alertGrp)
        {
            alertRows.AppendLine(BuildAlertRow(alertGrp.First(), links));
        }
    }

    private static string BuildAlertRow(DatabaseAlert alert, List<(string Title, string Database, string Links)> links)
    {
        var severityColor = alert.Severity switch
        {
            DatabaseAlert.SeverityLevel.Critical => "red",
            DatabaseAlert.SeverityLevel.Error => "orange",
            DatabaseAlert.SeverityLevel.Warning => "yellow",
            DatabaseAlert.SeverityLevel.Info => "lightgreen",
            _ => "white" // Default background color
        };

        return $@"
                <tr>
                    <td style='border: 1px solid black;'>{alert.Database}</td>
                    <td style='border: 1px solid black; background-color:{severityColor};'>{alert.Severity}</td>
                    <td style='border: 1px solid black;text-align:center;'> 
                    {
                        MergeSourceLinks(
                            [.. links.Where(x => x.Title == alert.Title && x.Database == alert.Database).Select(x => x.Links)])
                    } </td>
                    <td style='border: 1px solid black;'>{alert.CreatedAt:yyyy-MM-dd HH:mm:ss}</td>
                    <td style='border: 1px solid black;'>{alert.Category ?? "N/A"}</td>
                    <td style='border: 1px solid black;'>{alert.Title}</td>
                    <td style='border: 1px solid black;'>{alert.Message}</td>
                </tr>";
    }
    
    private static List<(string Title, string Database, string Link)> BuildSourceLinks(IOrderedEnumerable<DatabaseAlert> sortedAlerts)
    {
        List<(string Title, string Database, string Link)> links = [];

        links.AddRange(from grp in sortedAlerts
             .GroupBy(alert => alert.Database)
                       from alert in grp
                       select (
                        alert.Title,
                        alert.Database,
                        $"<a href=\"{alert.Server}\">{alert.FriendlyServerSubDomainName}</a>"));
        return links;
    }

    private static string MergeSourceLinks(string[] links)
    {
        var result = new StringBuilder();

        foreach (var link in links)
        {
            result.Append($"{link} ");
        }

        return result.ToString();
    }
}

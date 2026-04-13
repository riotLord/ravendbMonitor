using Azure;
using Azure.Communication.Email;

namespace Servercyde.Monitoring.Core.Email;

public record MailMessage(string Subject, string From, string To, string HtmlContent);
public record EmailSendReceipt(string OperationId, string Status);

public interface IEmailService
{
    Task<EmailSendReceipt> SendEmailAsync(MailMessage message, CancellationToken cancellationToken = default);
}

public interface IEmailClient
{
    Task<EmailSendReceipt> SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}

public class EmailService(IEmailClient client) : IEmailService
{
    private readonly IEmailClient _client = client;

    public Task<EmailSendReceipt> SendEmailAsync(MailMessage message, CancellationToken cancellationToken = default)
        => _client.SendAsync(message, cancellationToken);
}

public class AzureCommunicationServicesEmailClient(EmailClient client) : IEmailClient
{
    private const string PlainTextFallback = "The content of this email is only provided as HTML";
    private readonly EmailClient _client = client;

    public async Task<EmailSendReceipt> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var sendOperation = await _client.SendAsync(
            WaitUntil.Completed,
            message.From,
            message.To,
            message.Subject,
            message.HtmlContent,
            PlainTextFallback,
            cancellationToken);

        var result = sendOperation.Value;
        if (!string.Equals(result.Status.ToString(), "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"ACS email send failed with status '{result.Status}'.");
        }

        return new EmailSendReceipt(sendOperation.Id, result.Status.ToString());
    }
}

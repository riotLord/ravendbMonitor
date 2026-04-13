using Servercyde.Monitoring.Core.Email;

namespace Servercyde.Monitoring.Tests.Fakes;

public class FakeEmailClient : IEmailClient
{
    private readonly List<Message> _messages = [];

    public IEnumerable<Message> Messages => _messages;

    public Task<EmailSendReceipt> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Add(new Message(message.To, message.Subject, message.HtmlContent, message.From));
        return Task.FromResult(new EmailSendReceipt(Guid.NewGuid().ToString(), "Succeeded"));
    }

    public record Message(
        string Recipient,
        string Subject,
        string Contents,
        string Sender
    );
}

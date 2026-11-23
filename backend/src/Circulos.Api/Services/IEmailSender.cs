using Circulos.Api.Data;
using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

public sealed class OutboxEmailSender(AppDbContext db, ILogger<OutboxEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        db.SentEmails.Add(new SentEmail
        {
            Id = Guid.NewGuid(),
            To = to,
            Subject = subject,
            Body = body,
            SentAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("E-mail para {To}: {Subject}", to, LocationPrivacy.Redact(subject));
    }
}

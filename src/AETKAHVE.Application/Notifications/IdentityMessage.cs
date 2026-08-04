namespace AETKAHVE.Application.Notifications;

public sealed record IdentityMessage(string Destination, string Subject, string HtmlBody);

public interface IIdentityMessageSender
{
    Task SendAsync(IdentityMessage message, CancellationToken cancellationToken = default);
}


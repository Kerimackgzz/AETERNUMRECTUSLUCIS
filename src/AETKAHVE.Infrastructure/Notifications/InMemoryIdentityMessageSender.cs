using System.Collections.Concurrent;
using AETKAHVE.Application.Notifications;

namespace AETKAHVE.Infrastructure.Notifications;

public sealed class InMemoryIdentityMessageSender : IIdentityMessageSender
{
    private readonly ConcurrentQueue<IdentityMessage> _messages = new();

    public IReadOnlyCollection<IdentityMessage> Messages => _messages.ToArray();

    public Task SendAsync(IdentityMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}


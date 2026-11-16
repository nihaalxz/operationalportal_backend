using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Common.Options;
using TexasOperationalPortal.Contracts.Notifications;

namespace TexasOperationalPortal.Infrastructure.Notifications;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        INotificationDispatcher dispatcher,
        IOptions<NotificationOptions> options,
        ILogger<NotificationPublisher> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        string eventName,
        string referenceType,
        string referenceId,
        long? headId = null,
        long? agentId = null,
        string? targetGroup = null,
        string priority = "Normal",
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Notifications disabled in options. Skipping publish for {EventName}.", eventName);
            return;
        }

        var envelope = new NotificationEnvelope
        {
            EventId = "evt_" + Guid.NewGuid().ToString("N"),
            CorrelationId = "tx-corr-" + Guid.NewGuid().ToString("N")[..12],
            EventName = eventName,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            HeadId = headId,
            AgentId = agentId,
            TargetGroup = targetGroup,
            Priority = priority,
            Producer = _options.ProducerName,
            Timestamp = DateTime.UtcNow
        };

        await PublishEnvelopeAsync(envelope, ct);
    }

    public async Task PublishEnvelopeAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        if (!_options.Enabled || envelope == null) return;

        _logger.LogInformation("Publishing notification envelope {EventName} ({RefType}:{RefId}). EventId: {EventId}",
            envelope.EventName, envelope.ReferenceType, envelope.ReferenceId, envelope.EventId);

        await _dispatcher.DispatchAsync(envelope, ct);
    }
}

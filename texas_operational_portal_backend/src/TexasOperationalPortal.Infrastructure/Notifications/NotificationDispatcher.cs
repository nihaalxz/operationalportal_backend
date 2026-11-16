using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Common.Options;
using TexasOperationalPortal.Contracts.Notifications;
using TexasOperationalPortal.Infrastructure.Hubs;

namespace TexasOperationalPortal.Infrastructure.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<TexasSyncHub> _hubContext;
    private readonly SignalROptions _signalROptions;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IHubContext<TexasSyncHub> hubContext,
        IOptions<SignalROptions> signalROptions,
        ILogger<NotificationDispatcher> logger)
    {
        _hubContext = hubContext;
        _signalROptions = signalROptions.Value;
        _logger = logger;
    }

    public async Task DispatchAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        if (envelope == null) return;

        _logger.LogInformation("Dispatching signal {EventName} for {RefType}:{RefId} to group {TargetGroup}. EventId: {EventId}, CorrelationId: {CorrelationId}",
            envelope.EventName, envelope.ReferenceType, envelope.ReferenceId, envelope.TargetGroup ?? "All", envelope.EventId, envelope.CorrelationId);

        if (!_signalROptions.Enabled)
        {
            _logger.LogDebug("SignalR is disabled in options. Skipping hub dispatch for EventId {EventId}.", envelope.EventId);
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(envelope.TargetGroup))
            {
                await _hubContext.Clients.Group(envelope.TargetGroup.Trim()).SendAsync("OnSignal", envelope, ct);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("OnSignal", envelope, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch SignalR notification for EventId {EventId}.", envelope.EventId);
            throw;
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Contracts.Notifications;

namespace TexasOperationalPortal.Application.Common.Interfaces;

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationEnvelope envelope, CancellationToken ct = default);
}

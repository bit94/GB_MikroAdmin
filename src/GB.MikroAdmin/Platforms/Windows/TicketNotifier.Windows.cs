using GB.MikroAdmin.Core.Models;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace GB.MikroAdmin.Services;

public sealed partial class TicketNotifier
{
    private const string Tag = "tickets-agotados";

    public Task EnsurePermissionAsync() => Task.CompletedTask;

    public Task NotifyExpiredAsync(IReadOnlyList<HotspotUser> tickets)
    {
        if (tickets.Count == 0)
            return Task.CompletedTask;

        try
        {
            var title = tickets.Count == 1
                ? $"El ticket {tickets[0].Name} se agotó"
                : $"{tickets.Count} tickets se agotaron";
            var text = tickets.Count == 1
                ? "Entra a Tickets para limpiarlo y liberar el cupo."
                : "Entra a Tickets para limpiar los agotados.";

            var notification = new AppNotificationBuilder()
                .AddArgument(NotificationNavigation.ExtraOpen, NotificationNavigation.OpenTickets)
                .AddText(title)
                .AddText(text)
                .BuildNotification();

            notification.Tag = Tag;
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Si Windows no admite notificaciones, la app sigue funcionando sin el aviso.
        }

        return Task.CompletedTask;
    }

    public async Task ClearExpiredAsync()
    {
        try
        {
            await AppNotificationManager.Default.RemoveByTagAsync(Tag);
        }
        catch
        {
        }
    }
}

using Android.App;
using Android.Content;
using Android.OS;
using GB.MikroAdmin.Core.Models;
using Microsoft.Maui.ApplicationModel;

namespace GB.MikroAdmin.Services;

public sealed partial class TicketNotifier
{
    private const string ChannelId = "tickets-agotados";
    private const int NotificationId = 4101;

    public async Task EnsurePermissionAsync()
    {
        EnsureChannel();

        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return;

        // Pedir permiso exige el hilo principal; el vigilante corre en segundo plano.
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        });
    }

    public Task NotifyExpiredAsync(IReadOnlyList<HotspotUser> tickets)
    {
        if (tickets.Count == 0)
            return Task.CompletedTask;

        EnsureChannel();

        var context = Platform.AppContext;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null)
            return Task.CompletedTask;

        var title = tickets.Count == 1
            ? $"El ticket {tickets[0].Name} se agotó"
            : $"{tickets.Count} tickets se agotaron";
        var text = tickets.Count == 1
            ? "Entra a Tickets para limpiarlo y liberar el cupo."
            : "Entra a Tickets para limpiar los agotados.";

        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        intent.PutExtra(NotificationNavigation.ExtraOpen, NotificationNavigation.OpenTickets);

        var pending = PendingIntent.GetActivity(
            context,
            NotificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notification = new Notification.Builder(context, ChannelId)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetStyle(new Notification.BigTextStyle().BigText(text))
            .SetSmallIcon(GB.MikroAdmin.Resource.Drawable.ic_ticket)
            .SetContentIntent(pending)
            .SetAutoCancel(true)
            .Build();

        manager.Notify(NotificationId, notification);
        return Task.CompletedTask;
    }

    public Task ClearExpiredAsync()
    {
        var manager = (NotificationManager?)Platform.AppContext.GetSystemService(Context.NotificationService);
        manager?.Cancel(NotificationId);
        return Task.CompletedTask;
    }

    private static void EnsureChannel()
    {
        var manager = (NotificationManager?)Platform.AppContext.GetSystemService(Context.NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) is not null)
            return;

        var channel = new NotificationChannel(ChannelId, "Tickets agotados", NotificationImportance.High)
        {
            Description = "Avisa cuando un ticket termina su tiempo."
        };
        manager?.CreateNotificationChannel(channel);
    }
}

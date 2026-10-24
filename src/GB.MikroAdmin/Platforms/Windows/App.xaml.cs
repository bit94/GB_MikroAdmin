using GB.MikroAdmin.Services;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Windows.AppNotifications;

namespace GB.MikroAdmin.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        try
        {
            // Sin paquete MSIX, la Windows App SDK debe registrar la app para poder mostrar
            // notificaciones y recibir el toque del usuario.
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
        }
        catch
        {
            // Si el registro falla, la app sigue funcionando; solo no habrá avisos del sistema.
        }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (args.Argument.Contains(NotificationNavigation.OpenTickets, StringComparison.OrdinalIgnoreCase))
            NotificationNavigation.OpenTicketsFromNotification();
    }
}

using GB.MikroAdmin.ViewModels;
using GB.MikroAdmin.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;

namespace GB.MikroAdmin.Services;

/// <summary>
/// Lleva al administrador a Tickets cuando toca la notificación de vencimiento, siempre que la
/// sesión con el router siga activa. Si la app se abrió de cero, el login sigue siendo la entrada.
/// </summary>
public static class NotificationNavigation
{
    public const string ExtraOpen = "open";
    public const string OpenTickets = "tickets";

    public static void OpenTicketsFromNotification()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // En arranque en frío el Shell todavía puede no existir cuando llega el toque.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (Shell.Current is { } shell)
                {
                    var connection = IPlatformApplication.Current?.Services?.GetService<ConnectionState>();
                    if (connection?.IsConnected == true)
                    {
                        try
                        {
                            await shell.GoToAsync(Routes.Users);
                        }
                        catch
                        {
                            // Si la navegación falla, la app queda en la pantalla actual.
                        }
                    }

                    return;
                }

                await Task.Delay(300);
            }
        });
    }
}

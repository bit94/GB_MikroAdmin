using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using GB.MikroAdmin.Services;

namespace GB.MikroAdmin;

/// <summary>
/// Punto de entrada de la aplicación en Android. Sin este atributo MainLauncher el APK se
/// instala pero el sistema no muestra ningún icono ni permite iniciarla, que es exactamente
/// el error que produce depurar sin esta clase.
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OpenTicketsIfRequested(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenTicketsIfRequested(intent);
    }

    /// <summary>La notificación de vencimiento trae la orden de abrir Tickets al tocarla.</summary>
    private static void OpenTicketsIfRequested(Intent? intent)
    {
        if (intent?.GetStringExtra(NotificationNavigation.ExtraOpen) != NotificationNavigation.OpenTickets)
            return;

        intent.RemoveExtra(NotificationNavigation.ExtraOpen);
        NotificationNavigation.OpenTicketsFromNotification();
    }
}

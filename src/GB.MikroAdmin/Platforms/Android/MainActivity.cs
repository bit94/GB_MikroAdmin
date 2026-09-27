using Android.App;
using Android.Content.PM;
using Android.OS;

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
}

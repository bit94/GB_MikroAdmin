#if WINDOWS
using WindowsColor = global::Windows.UI.Color;
#endif

namespace GB.MikroAdmin;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? state)
    {
        var shell = new AppShell(_services);

        // El menú se construye al crear la ventana, no en el constructor del AppShell. Las páginas
        // usan StaticResource de App.xaml y ese diccionario solo está disponible una vez cargado
        // App: construirlas antes lanzaba "StaticResource not found for key Caption" al arrancar.
        shell.BuildMenu();

        var window = new Window(shell) { Title = "Mikro Admin" };
#if WINDOWS
        window.HandlerChanged += (_, _) => ApplyWindowsChrome(window);
#endif
        return window;
    }

#if WINDOWS
    private static void ApplyWindowsChrome(Window window)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            return;

        // Sin paquete MSIX la barra de tareas toma el icono de la ventana; se fija en tiempo de
        // ejecución para no depender de que el icono quede embebido en el ejecutable.
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "appicon.ico");
        if (File.Exists(iconPath))
            nativeWindow.AppWindow.SetIcon(iconPath);

        var titleBar = nativeWindow.AppWindow.TitleBar;
        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            return;

        var background = WindowsColor.FromArgb(255, 11, 22, 38);
        var foreground = WindowsColor.FromArgb(255, 248, 250, 252);
        var hover = WindowsColor.FromArgb(255, 19, 32, 51);
        var pressed = WindowsColor.FromArgb(255, 27, 43, 68);
        var inactive = WindowsColor.FromArgb(255, 148, 163, 184);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactive;
    }
#endif
}

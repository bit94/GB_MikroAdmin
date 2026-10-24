using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;
using GB.MikroAdmin.ViewModels;
using GB.MikroAdmin.Views;

namespace GB.MikroAdmin;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if WINDOWS
        // La barra superior nativa de Shell siempre dibuja el título junto al icono. Se cambia su
        // plantilla por una que muestre solo el icono, conservando el título en el tooltip y en
        // las propiedades de accesibilidad.
        Platforms.Windows.IconOnlyTabBar.Apply();

        // En Windows, MAUI dibuja Picker.Title como encabezado interno del ComboBox y duplica la
        // etiqueta del formulario. Se mueve al placeholder, como ocurre en Android.
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("TitleAsPlaceholder", (handler, view) =>
        {
            handler.PlatformView.Header = null;
            handler.PlatformView.PlaceholderText = view.Title;
        });
#endif

#if ANDROID
        // La barra inferior nativa dibuja icono y texto; se ocultan las etiquetas desde el
        // tracker de apariencia para conservar los títulos como descripción accesible.
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<Shell, Platforms.Android.IconOnlyShellRenderer>());
#endif

        // --- Infraestructura ---------------------------------------------------------------
        builder.Services.AddSingleton<IRouterSettingsStore, RouterSettingsStore>();
        builder.Services.AddSingleton<ITikSessionFactory, Tik4NetSessionFactory>();

        // El modo real conecta con API-SSL y la fábrica enlaza la validación de certificado al
        // socket que autentica; no hay un handshake de comprobación separado.
        builder.Services.AddSingleton<IRouterTransportFactory>(_ => new RouterTransportFactory(simulated =>
            simulated
                ? new TickFakeConnection()
                : new Tik4NetRouterTransport(_.GetRequiredService<ITikSessionFactory>())));

        builder.Services.AddSingleton<IRouterService, RouterService>();
        builder.Services.AddSingleton<ITicketSource>(sp => sp.GetRequiredService<IRouterService>());
        builder.Services.AddSingleton<IRouterUserWriter>(sp => (RouterService)sp.GetRequiredService<IRouterService>());
        builder.Services.AddSingleton<ITicketCodeGenerator, NumericTicketCodeGenerator>();
        builder.Services.AddSingleton<IHotspotProvisioningService, HotspotProvisioningService>();
        builder.Services.AddSingleton<ITicketHistoryStore>(_ => new TicketHistoryStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<IFileExporter, MauiFileExporter>();

        // La vigilancia consulta el router mientras la sesión siga activa. Sin un servidor no hay
        // push real: el aviso nace en el dispositivo que tiene la conexión.
        builder.Services.AddSingleton<ITicketNotifier, TicketNotifier>();
        builder.Services.AddSingleton(sp => new TicketExpiryMonitor(
            sp.GetRequiredService<ITicketSource>(),
            sp.GetRequiredService<ITicketNotifier>(),
            new TicketExpiryTracker(),
            TimeSpan.FromSeconds(30)));

        // --- Estado compartido -------------------------------------------------------------
        builder.Services.AddSingleton<ConnectionState>();

        // --- ViewModels --------------------------------------------------------------------
        // Las páginas se crean una sola vez en AppShell, así que sus ViewModels también deben
        // ser únicos: con AddTransient cada página tendría un ViewModel distinto del que la
        // alimenta y las notificaciones se perderían.
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<MainMenuViewModel>();
        builder.Services.AddSingleton<UsersViewModel>();
        builder.Services.AddSingleton<ProfilesViewModel>();
        builder.Services.AddSingleton<InterfacesViewModel>();
        builder.Services.AddSingleton<TicketHistoryViewModel>();

        // --- Páginas -----------------------------------------------------------------------
        // Se registran a partir de ShellMenu, la misma lista que usa AppShell para construir el
        // menú. Antes había dos listas paralelas y añadir una sección sin registrar su página no
        // fallaba al compilar, sino al arrancar: "No service for type ... has been registered".
        foreach (var pageType in ShellMenu.AllPageTypes)
            builder.Services.AddSingleton(pageType);

        // Comprobación de arranque: si una sección del menú apunta a una página sin registrar, el
        // AppShell reventaría al crearse con un mensaje que ni siquiera dice cuál falta. Se revisan
        // los descriptores del contenedor, sin construir nada: resolver un servicio lo instanciaría
        // y las páginas aún no se pueden crear (sus StaticResource viven en App.xaml, sin cargar).
        ShellMenu.VerifyPagesRegistered(builder.Services);

        return builder.Build();
    }
}

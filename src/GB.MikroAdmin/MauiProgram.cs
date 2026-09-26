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

        // --- Infraestructura ---------------------------------------------------------------
        builder.Services.AddSingleton<IRouterSettingsStore, RouterSettingsStore>();
        builder.Services.AddSingleton<ITikSessionFactory, Tik4NetSessionFactory>();

        // El transporte real se construye con la validación de certificado delante, de modo que
        // ninguna conexión llega a tik4net sin haber pasado la comprobación de la cadena y el SAN.
        builder.Services.AddSingleton<IRouterTransportFactory>(_ => new RouterTransportFactory(simulated =>
            simulated
                ? new TickFakeConnection()
                : new Tik4NetRouterTransport(
                    _.GetRequiredService<ITikSessionFactory>(),
                    (settings, ct) => RouterCertificateValidator.ValidateAsync(settings.Host, settings.Port, ct))));

        builder.Services.AddSingleton<IRouterService, RouterService>();
        builder.Services.AddSingleton<IRouterUserWriter>(sp => (RouterService)sp.GetRequiredService<IRouterService>());
        builder.Services.AddSingleton<IVoucherCodeGenerator, NumericVoucherCodeGenerator>();
        builder.Services.AddSingleton<IHotspotProvisioningService, HotspotProvisioningService>();
        builder.Services.AddSingleton<IVoucherHistoryStore, VoucherHistoryStore>();
        builder.Services.AddSingleton<IFileExporter, MauiFileExporter>();

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
        builder.Services.AddSingleton<VoucherHistoryViewModel>();

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

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
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<MainMenuPage>();
        builder.Services.AddSingleton<UsersPage>();
        builder.Services.AddSingleton<ProfilesPage>();
        builder.Services.AddSingleton<InterfacesPage>();
        builder.Services.AddSingleton<VoucherHistoryPage>();

        return builder.Build();
    }
}

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
        builder.Services.AddSingleton<IRouterSettingsStore, RouterSettingsStore>();
        builder.Services.AddSingleton<IRouterTransportFactory, RouterTransportFactory>();
        builder.Services.AddSingleton<IRouterService, RouterService>();
        builder.Services.AddSingleton<IHotspotProvisioningService, HotspotProvisioningService>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<MainMenuViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainMenuPage>();
        builder.Services.AddTransient<InterfacesPage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<ProfilesPage>();

        return builder.Build();
    }
}

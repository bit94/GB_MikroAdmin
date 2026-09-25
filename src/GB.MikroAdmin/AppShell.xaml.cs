using GB.MikroAdmin.Views;
namespace GB.MikroAdmin;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        var login = new ShellContent
        {
            Route = "login",
            Content = services.GetRequiredService<LoginPage>()
        };
        Shell.SetFlyoutBehavior(login, FlyoutBehavior.Disabled);
        Items.Add(login);

        AddMenuItem("Inicio", "home", services.GetRequiredService<MainMenuPage>());
        AddMenuItem("Interfaces", "interfaces", services.GetRequiredService<InterfacesPage>());
        AddMenuItem("Usuarios Hotspot", "users", services.GetRequiredService<UsersPage>());
        AddMenuItem("Perfiles", "profiles", services.GetRequiredService<ProfilesPage>());
    }

    private void AddMenuItem(string title, string route, Page page)
    {
        Items.Add(new ShellContent { Title = title, Route = route, Content = page });
    }
}

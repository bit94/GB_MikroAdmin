using GB.MikroAdmin.Views;

namespace GB.MikroAdmin;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // El login va fuera del menú: es la pantalla de entrada y no debe verse en el flyout.
        var login = new ShellContent
        {
            Route = Routes.Login.TrimStart('/'),
            Content = services.GetRequiredService<LoginPage>()
        };
        Shell.SetFlyoutBehavior(login, FlyoutBehavior.Disabled);
        Items.Add(login);

        Items.Add(BuildSection("MikroAdmin",
            ("Inicio", "home", services.GetRequiredService<MainMenuPage>()),
            ("Vouchers", "users", services.GetRequiredService<UsersPage>()),
            ("Generar", "generate", services.GetRequiredService<GeneratePage>()),
            ("Perfiles", "profiles", services.GetRequiredService<ProfilesPage>()),
            ("Interfaces", "interfaces", services.GetRequiredService<InterfacesPage>()),
            ("Histórico", "history", services.GetRequiredService<VoucherHistoryPage>())));
    }

    private static ShellItem BuildSection(string title, params (string Title, string Route, Page Page)[] pages)
    {
        var section = new ShellItem { Title = title };
        foreach (var (pageTitle, route, page) in pages)
            section.Items.Add(new ShellContent { Title = pageTitle, Route = route, Content = page });
        return section;
    }
}

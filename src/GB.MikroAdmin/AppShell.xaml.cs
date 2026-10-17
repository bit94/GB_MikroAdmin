using GB.MikroAdmin.Views;

namespace GB.MikroAdmin;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    /// <summary>
    /// Construye el menú. Se llama desde <see cref="App"/> una vez creada la ventana, no desde el
    /// constructor: las vistas resuelven <c>StaticResource</c> de <c>App.xaml</c>, y mientras
    /// <c>Application.Current</c> es nulo ese diccionario no existe. Construirlas antes lanzaba
    /// "StaticResource not found for key Caption" y la aplicación moría al arrancar.
    /// </summary>
    public void BuildMenu()
    {
        if (Items.Count > 0)
            return;

        // Las entradas salen de ShellMenu, la misma lista que MauiProgram usa para registrar las
        // páginas: si se añade una sección, no puede quedar sin su página registrada.
        var login = BuildContent(ShellMenu.Login);

        // El flyout está desactivado, así que el login no aparece en ningún menú lateral. Sigue
        // siendo un ShellItem para poder volver a él con la ruta absoluta //login al desconectar.
        Items.Add(login);

        Items.Add(BuildSection("Mikro Admin", ShellMenu.Sections));
    }

    private ShellItem BuildSection(string title, IReadOnlyList<ShellMenu.Entry> entries)
    {
        var section = new ShellItem { Title = title };
        foreach (var entry in entries)
            section.Items.Add(BuildContent(entry));
        return section;
    }

    private ShellContent BuildContent(ShellMenu.Entry entry)
    {
        var content = new ShellContent
        {
            Title = entry.Title,
            Route = entry.Route,
            ContentTemplate = new DataTemplate(() => (Page)_services.GetRequiredService(entry.PageType))
        };

        if (!string.IsNullOrEmpty(entry.Icon))
            content.Icon = entry.Icon;

        return content;
    }
}

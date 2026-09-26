using GB.MikroAdmin.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GB.MikroAdmin.Views;

/// <summary>
/// Rutas de navegación en un solo lugar. Antes se escribían como cadenas sueltas y dos de
/// ellas (<c>//interfaces</c>, <c>//users</c>) no existían realmente, así que los botones del
/// menú de inicio no navegaban.
/// </summary>
public static class Routes
{
    public const string Login = "login";
    public const string Home = "//home";
    public const string Interfaces = "//interfaces";
    public const string Users = "//users";
    public const string Generate = "//generate";
    public const string Profiles = "//profiles";
    public const string History = "//history";
}

/// <summary>
/// Definición del menú lateral. Es la única fuente de verdad de qué páginas construye el
/// <c>AppShell</c>: la lista se recorre tanto para registrar las páginas en el contenedor de
/// dependencias como para crear las secciones, de modo que no pueden desincronizarse.
/// </summary>
public static class ShellMenu
{
    /// <summary>Una entrada del menú: título visible, ruta y tipo de página.</summary>
    public readonly record struct Entry(string Title, string Route, Type PageType);
    /// <summary>
    /// Pantalla de entrada. Va fuera del menú: no debe aparecer en el desplegable lateral.
    /// </summary>
    public static Entry Login { get; } = new("Entrar", Routes.Login, typeof(LoginPage));

    /// <summary>Secciones del menú, en orden de aparición.</summary>
    public static IReadOnlyList<Entry> Sections { get; } =
    [
        new("Inicio", "home", typeof(MainMenuPage)),
        new("Vouchers", "users", typeof(UsersPage)),
        new("Generar", "generate", typeof(GeneratePage)),
        new("Perfiles", "profiles", typeof(ProfilesPage)),
        new("Interfaces", "interfaces", typeof(InterfacesPage)),
        new("Histórico", "history", typeof(VoucherHistoryPage)),
    ];

    /// <summary>Todas las páginas que el shell necesita resolver, para registrarlas de una vez.</summary>
    public static IEnumerable<Type> AllPageTypes =>
        new[] { Login.PageType }.Concat(Sections.Select(entry => entry.PageType));

    /// <summary>
    /// Comprueba que cada página del menú está registrada, mirando los descriptores del contenedor
    /// y sin construir nada. Es importante no instanciar aquí: aunque la comprobación solo
    /// pregunte si el servicio existe, resolverlo crea la página, y una página no se puede crear
    /// todavía porque sus <c>StaticResource</c> viven en <c>App.xaml</c>, que aún no se ha cargado.
    /// </summary>
    /// <exception cref="InvalidOperationException">Con la lista de páginas sin registrar.</exception>
    public static void VerifyPagesRegistered(IServiceCollection services)
    {
        var registered = services
            .Select(descriptor => descriptor.ServiceType)
            .ToHashSet();

        var missing = new[] { Login }
            .Concat(Sections)
            .Where(entry => !registered.Contains(entry.PageType))
            .Select(entry => $"  · «{entry.Title}» ({entry.PageType.Name})")
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Hay páginas del menú sin registrar en MauiProgram. El menú las usa, así que la " +
                "aplicación fallaría al arrancar:" + Environment.NewLine +
                string.Join(Environment.NewLine, missing));
        }
    }
}

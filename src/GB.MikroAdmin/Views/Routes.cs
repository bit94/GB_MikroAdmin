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

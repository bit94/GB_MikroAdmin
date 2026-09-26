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

        return new Window(shell);
    }
}

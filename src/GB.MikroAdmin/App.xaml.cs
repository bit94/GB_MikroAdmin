namespace GB.MikroAdmin;

public partial class App : Application
{
    readonly IServiceProvider _services;
    public App(IServiceProvider services) { _services = services; InitializeComponent(); }
    protected override Window CreateWindow(IActivationState? state) => new(new AppShell(_services));
}

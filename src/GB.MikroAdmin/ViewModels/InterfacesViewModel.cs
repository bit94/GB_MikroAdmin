using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>Estado de las interfaces del router.</summary>
public sealed class InterfacesViewModel : PageViewModel
{
    private readonly IRouterService _router;

    public InterfacesViewModel(IRouterService router, ConnectionState connection) : base(connection)
    {
        _router = router;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public ObservableCollection<RouterInterface> Interfaces { get; } = [];

    public string Summary => Interfaces.Count == 0
        ? "Sin datos de interfaces."
        : $"{Interfaces.Count(i => i.Running)} de {Interfaces.Count} interfaces activas";

    public ICommand RefreshCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        if (!Connection.IsConnected)
        {
            Warn("No hay conexión activa con el router.");
            return;
        }

        var interfaces = await _router.GetInterfacesAsync();
        Interfaces.Clear();
        foreach (var item in interfaces)
            Interfaces.Add(item);

        Raise(nameof(Summary));
    });
}

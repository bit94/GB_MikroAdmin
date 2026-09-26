using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Conecta bajo demanda y delega en el transporte activo. Los métodos de escritura se exponen
/// de forma explícita para que la UI no dependa de la interfaz completa del transporte.
/// </summary>
public sealed class RouterService : IRouterService, IRouterUserWriter
{
    private readonly IRouterSettingsStore _store;
    private readonly IRouterTransportFactory _factory;
    private IRouterTransport? _transport;

    public RouterService(IRouterSettingsStore store, IRouterTransportFactory factory)
    {
        _store = store;
        _factory = factory;
    }

    public bool IsConnected => _transport is not null;

    public string? ConnectedHost { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _store.GetAsync()
            ?? throw new InvalidOperationException("Configura el router antes de conectar.");

        await DisposeTransportAsync();
        _transport = _factory.Create(settings.Simulated);
        try
        {
            await _transport.ConnectAsync(settings, cancellationToken);
        }
        catch
        {
            await DisposeTransportAsync();
            throw;
        }

        ConnectedHost = $"{settings.Host}:{settings.Port}{(settings.Simulated ? " (simulado)" : string.Empty)}";
    }

    public Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken cancellationToken = default)
        => Connected().InterfacesAsync(cancellationToken);

    public Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        => Connected().UsersAsync(cancellationToken);

    public Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        => Connected().ProfilesAsync(cancellationToken);

    public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
        => Connected().AddUserAsync(name, password, profile, limit, cancellationToken);

    public Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken cancellationToken = default)
        => Connected().AddProfileAsync(name, rateLimit, voucherDuration, cancellationToken);

    public Task RemoveUserAsync(string name, CancellationToken cancellationToken = default)
        => Connected().RemoveUserAsync(name, cancellationToken);

    public async Task DisconnectAsync()
    {
        await DisposeTransportAsync();
        ConnectedHost = null;
    }

    private IRouterTransport Connected()
        => _transport ?? throw new InvalidOperationException("No hay conexión activa. Vuelve a la pantalla de conexión.");

    private async ValueTask DisposeTransportAsync()
    {
        if (_transport is null)
            return;
        await _transport.DisposeAsync();
        _transport = null;
    }

    public async ValueTask DisposeAsync() => await DisposeTransportAsync();
}

public interface IRouterService : IAsyncDisposable
{
    bool IsConnected { get; }
    string? ConnectedHost { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default);
    Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken cancellationToken = default);
    Task RemoveUserAsync(string name, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public sealed class RouterTransportFactory : IRouterTransportFactory
{
    private readonly Func<bool, IRouterTransport> _create;

    public RouterTransportFactory(Func<bool, IRouterTransport> create) => _create = create;

    public IRouterTransport Create(bool simulated) => _create(simulated);
}

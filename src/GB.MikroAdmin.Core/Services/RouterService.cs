using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Conecta bajo demanda y delega en el transporte activo. Los métodos de escritura se exponen
/// de forma explícita para que la UI no dependa de la interfaz completa del transporte.
/// </summary>
/// <remarks>
/// Todas las operaciones pasan por un semáforo: una sesión de RouterOS atiende un comando a la
/// vez y la vigilancia de vencimientos consulta el router en paralelo con la pantalla de Tickets.
/// Sin esta serialización, dos listados simultáneos podían mezclar respuestas de la API.
/// </remarks>
public sealed class RouterService : IRouterService, IRouterUserWriter
{
    private readonly IRouterSettingsStore _store;
    private readonly IRouterTransportFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
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
        await _gate.WaitAsync(cancellationToken);
        try
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
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Connected().InterfacesAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Connected().UsersAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Connected().ProfilesAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Connected().AddUserAsync(name, password, profile, limit, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Connected().AddProfileAsync(name, rateLimit, ticketDuration, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Connected().UpdateProfileAsync(name, rateLimit, ticketDuration, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Connected().RemoveProfileAsync(name, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Connected().RemoveUsersAsync(names, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await DisposeTransportAsync();
            ConnectedHost = null;
        }
        finally
        {
            _gate.Release();
        }
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

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}

public interface IRouterService : ITicketSource, IAsyncDisposable
{
    string? ConnectedHost { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default);
    Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken = default);
    Task<int> RemoveProfileAsync(string name, CancellationToken cancellationToken = default);
    Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public sealed class RouterTransportFactory : IRouterTransportFactory
{
    private readonly Func<bool, IRouterTransport> _create;

    public RouterTransportFactory(Func<bool, IRouterTransport> create) => _create = create;

    public IRouterTransport Create(bool simulated) => _create(simulated);
}

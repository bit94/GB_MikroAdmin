using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Escritura de usuarios en el router. Se declara estrecha a propósito: el servicio de
/// aprovisionamiento no necesita leer nada del equipo para crear un lote.
/// </summary>
public interface IRouterUserWriter
{
    Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default);
}

public interface IRouterSettingsStore
{
    Task SaveAsync(RouterSettings settings);
    Task<RouterSettings?> GetAsync();
    void Clear();
}

public interface IRouterTransport : IAsyncDisposable
{
    Task ConnectAsync(RouterSettings settings, CancellationToken cancellationToken);
    Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken cancellationToken);
    Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken);
    Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken);
    Task UpdateProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken);
    Task<int> RemoveProfileAsync(string name, CancellationToken cancellationToken);
    Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken);
}

public interface IRouterTransportFactory
{
    IRouterTransport Create(bool simulated);
}

/// <summary>
/// Lectura mínima de tickets para vigilar vencimientos. Se separa de <see cref="IRouterService"/>
/// para que el vigilante no dependa de las operaciones de escritura ni de la UI.
/// </summary>
public interface ITicketSource
{
    bool IsConnected { get; }
    Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default);
}

/// <summary>Aviso al administrador cuando un ticket agota su tiempo.</summary>
public interface ITicketNotifier
{
    Task EnsurePermissionAsync();
    Task NotifyExpiredAsync(IReadOnlyList<HotspotUser> tickets);
    Task ClearExpiredAsync();
}

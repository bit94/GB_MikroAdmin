using GB.MikroAdmin.Models;
namespace GB.MikroAdmin.Services;

public interface IRouterTransport : IAsyncDisposable { Task ConnectAsync(RouterSettings settings, CancellationToken ct); Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken ct); Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken ct); Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken ct); Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken ct); Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken ct); Task RemoveUserAsync(string name, CancellationToken ct); }
public interface IRouterTransportFactory { IRouterTransport Create(bool simulated); }
public sealed class RouterTransportFactory : IRouterTransportFactory { public IRouterTransport Create(bool simulated) => simulated ? new TikFakeConnection() : new Tik4NetRouterTransport(); }
public sealed class RouterService(IRouterSettingsStore store, IRouterTransportFactory factory) : IRouterService
{
    IRouterTransport? _transport;
    public async Task ConnectAsync(CancellationToken ct = default) { var s = await store.GetAsync() ?? throw new InvalidOperationException("Configura el router antes de conectar."); if (_transport is not null) await _transport.DisposeAsync(); _transport = factory.Create(s.Simulated); await _transport.ConnectAsync(s, ct); }
    IRouterTransport Connected() => _transport ?? throw new InvalidOperationException("No hay conexión activa.");
    public Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken ct = default) => Connected().InterfacesAsync(ct);
    public Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken ct = default) => Connected().UsersAsync(ct);
    public Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken ct = default) => Connected().ProfilesAsync(ct);
    public Task AddUserAsync(string n, string p, string profile, TimeSpan limit, CancellationToken ct = default) => Connected().AddUserAsync(n, p, profile, limit, ct);
    public Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken ct = default) => Connected().AddProfileAsync(name, rateLimit, voucherDuration, ct);
    public Task RemoveUserAsync(string n, CancellationToken ct = default) => Connected().RemoveUserAsync(n, ct);
    public async ValueTask DisposeAsync() { if (_transport is not null) await _transport.DisposeAsync(); }
}

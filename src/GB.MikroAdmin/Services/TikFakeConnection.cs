using GB.MikroAdmin.Models;
namespace GB.MikroAdmin.Services;
/// <summary>Implementación en memoria para pruebas y demostraciones; no abre sockets.</summary>
public sealed class TikFakeConnection : IRouterTransport
{
    readonly List<HotspotUser> _users = [new("demo", "limitado", TimeSpan.FromHours(1), TimeSpan.FromMinutes(12), true)];
 readonly List<HotspotProfile> _profiles = [new("default", null, null, 1), new("limitado", "2M/2M", TimeSpan.FromHours(1), 1)];
    public Task ConnectAsync(RouterSettings settings, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RouterInterface>>([new("ether1", true, false, "ether"), new("wlan1", true, false, "wlan"), new("bridge", true, false, "bridge")]);
    public Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotspotUser>>(_users.ToList());
    public Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotspotProfile>>(_profiles.ToList());
    public Task AddUserAsync(string n, string p, string profile, TimeSpan limit, CancellationToken ct) { if (_users.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("El usuario ya existe."); _users.Add(new(n, profile, limit, TimeSpan.Zero, false)); return Task.CompletedTask; }
 public Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken ct) { if (_profiles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("El perfil ya existe."); _profiles.Add(new(name, rateLimit, voucherDuration, 1)); return Task.CompletedTask; }
    public Task RemoveUserAsync(string n, CancellationToken ct) { _users.RemoveAll(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)); return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

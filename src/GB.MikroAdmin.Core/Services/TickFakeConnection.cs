using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Implementación en memoria para pruebas y demostraciones; no abre sockets.
/// Simula el consumo de los tickets con el tiempo real transcurrido desde su creación.
/// </summary>
public sealed class TickFakeConnection : IRouterTransport
{
    private readonly List<FakeUser> _users = [];
    private readonly List<HotspotProfile> _profiles =
    [
        new("default", null, null, 1),
        new("limitado", "2M/2M", TimeSpan.FromHours(1), 1)
    ];

    public int UserCount => _users.Count;

    public Task ConnectAsync(RouterSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RouterInterface>>(
        [
            new("ether1", true, false, "ether"),
            new("wlan1", true, false, "wlan"),
            new("bridge", true, false, "bridge")
        ]);

    public Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult<IReadOnlyList<HotspotUser>>(
            _users.Select(user => user.Snapshot(now)).ToList());
    }

    public Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<HotspotProfile>>(_profiles.ToList());

    public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken)
    {
        if (_users.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"El usuario «{name}» ya existe.");

        _users.Add(new FakeUser(name, profile, limit, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken)
    {
        if (_profiles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"El perfil «{name}» ya existe.");

        _profiles.Add(new HotspotProfile(name, rateLimit, ticketDuration, 1));
        return Task.CompletedTask;
    }

    public Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(_users.RemoveAll(x => wanted.Contains(x.Name)));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record FakeUser(string Name, string Profile, TimeSpan Limit, DateTimeOffset CreatedAt)
    {
        /// <summary>El consumo simulado sale del reloj para que la UI muestre avance sin tocar el router.</summary>
        public HotspotUser Snapshot(DateTimeOffset now)
        {
            var uptime = now - CreatedAt;
            if (uptime < TimeSpan.Zero)
                uptime = TimeSpan.Zero;
            return new HotspotUser(Name, Profile, Limit, uptime, uptime > TimeSpan.Zero);
        }
    }
}

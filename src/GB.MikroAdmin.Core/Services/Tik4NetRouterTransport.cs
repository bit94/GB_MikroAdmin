using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Adaptador de tik4net. Todo el contacto con la API de RouterOS vive aquí para que ni los
/// ViewModels ni la UI conozcan sus comandos.
/// </summary>
public sealed class Tik4NetRouterTransport : IRouterTransport
{
    private readonly Func<RouterSettings, CancellationToken, Task>? _certificateCheck;
    private ITikSession? _session;

    public Tik4NetRouterTransport(ITikSessionFactory sessionFactory, Func<RouterSettings, CancellationToken, Task>? certificateCheck = null)
    {
        SessionFactory = sessionFactory;
        _certificateCheck = certificateCheck;
    }

    public ITikSessionFactory SessionFactory { get; }

    public async Task ConnectAsync(RouterSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.UserName))
            throw new ArgumentException("Host y usuario son obligatorios.");
        if (!settings.UseTls)
            throw new InvalidOperationException("El modo real exige API-SSL/TLS. Actívalo en el router y usa normalmente el puerto 8729.");

        await DisposeSessionAsync();

        // Se valida la cadena, la revocación y el SAN antes de entregar la conexión a tik4net.
        if (_certificateCheck is not null)
            await _certificateCheck(settings, cancellationToken);

        _session = await SessionFactory.OpenAsync(settings, cancellationToken);
    }

    public Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken cancellationToken)
    {
        var rows = Connected().List("/interface/print");
        return Task.FromResult<IReadOnlyList<RouterInterface>>(
            rows.Select(r => new RouterInterface(
                r.GetString("name"),
                r.GetBool("running"),
                r.GetBool("disabled"),
                r.GetString("type"))).ToList());
    }

    public Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken cancellationToken)
    {
        var rows = Connected().List("/ip/hotspot/user/print");
        return Task.FromResult<IReadOnlyList<HotspotUser>>(
            rows.Select(r =>
            {
                // Un limit-uptime ilegible lanza; es preferible a mostrar un voucher como ilimitado.
                var limit = RouterOsTime.Parse(r.GetString("limit-uptime"));
                var uptime = RouterOsTime.Parse(r.GetString("uptime")) ?? TimeSpan.Zero;
                return new HotspotUser(
                    r.GetString("name"),
                    r.GetStringOrNull("profile") ?? "default",
                    limit,
                    uptime,
                    uptime > TimeSpan.Zero);
            }).ToList());
    }

    public Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken cancellationToken)
    {
        var rows = Connected().List("/ip/hotspot/user/profile/print");
        return Task.FromResult<IReadOnlyList<HotspotProfile>>(
            rows.Select(r => new HotspotProfile(
                r.GetString("name"),
                r.GetStringOrNull("rate-limit"),
                ProfileMarker.ReadVoucherDuration(r.GetStringOrNull("comment")),
                r.GetInt("shared-users", 1))).ToList());
    }

    public async Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken)
        => await Connected().ExecuteAsync("/ip/hotspot/user/add",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["password"] = password,
                ["profile"] = profile,
                ["limit-uptime"] = RouterOsTime.ToRouterOs(limit)
            });

    public async Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken cancellationToken)
        => await Connected().ExecuteAsync("/ip/hotspot/user/profile/add",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["rate-limit"] = rateLimit,
                ["shared-users"] = "1",
                ["comment"] = ProfileMarker.Build(voucherDuration)
            });

    /// <summary>
    /// Elimina por <c>.id</c>. El parámetro <c>numbers</c> de RouterOS acepta índices o rangos,
    /// no nombres, así que pasar el nombre hacía fallar el borrado contra un equipo real.
    /// </summary>
    public async Task RemoveUserAsync(string name, CancellationToken cancellationToken)
    {
        var session = Connected();
        var id = session.List("/ip/hotspot/user/print")
            .Select(r => (Id: r.GetString(".id"), Name: r.GetString("name")))
            .FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Id;

        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException($"El usuario «{name}» ya no existe en el router.");

        await session.ExecuteAsync("/ip/hotspot/user/remove", new Dictionary<string, string> { [".id"] = id });
    }

    private ITikSession Connected()
        => _session ?? throw new InvalidOperationException("No hay conexión activa.");

    private async ValueTask DisposeSessionAsync()
    {
        if (_session is null)
            return;
        await _session.DisposeAsync();
        _session = null;
    }

    public async ValueTask DisposeAsync() => await DisposeSessionAsync();
}

/// <summary>Fila de RouterOS con acceso tolerante a campos ausentes vistos como <c>dynamic</c>.</summary>
public interface ITikRow
{
    string GetString(string field);
    string? GetStringOrNull(string field);
    bool GetBool(string field);
    int GetInt(string field, int fallback);
}

public interface ITikSession : IAsyncDisposable
{
    IReadOnlyList<ITikRow> List(string command);
    Task ExecuteAsync(string command, IReadOnlyDictionary<string, string> parameters);
}

public interface ITikSessionFactory
{
    Task<ITikSession> OpenAsync(RouterSettings settings, CancellationToken cancellationToken);
}

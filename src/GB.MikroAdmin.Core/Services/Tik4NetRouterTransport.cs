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

    public async Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken cancellationToken)
    {
        var rows = await Connected().ListAsync("/interface/print", cancellationToken);
        return rows.Select(r => new RouterInterface(
            r.GetString("name"),
            r.GetBool("running"),
            r.GetBool("disabled"),
            r.GetString("type"))).ToList();
    }

    public async Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken cancellationToken)
    {
        var rows = await Connected().ListAsync("/ip/hotspot/user/print", cancellationToken);
        return rows.Select(r =>
        {
            // Un limit-uptime ilegible lanza; es preferible a mostrar un ticket como ilimitado.
            var limit = RouterOsTime.Parse(r.GetString("limit-uptime"));
            var uptime = RouterOsTime.Parse(r.GetString("uptime")) ?? TimeSpan.Zero;
            return new HotspotUser(
                r.GetString("name"),
                r.GetStringOrNull("profile") ?? "default",
                limit,
                uptime,
                uptime > TimeSpan.Zero);
        }).ToList();
    }

    public async Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken cancellationToken)
    {
        var rows = await Connected().ListAsync("/ip/hotspot/user/profile/print", cancellationToken);
        return rows.Select(r => new HotspotProfile(
            r.GetString("name"),
            r.GetStringOrNull("rate-limit"),
            ProfileMarker.ReadTicketDuration(r.GetStringOrNull("comment")),
            r.GetInt("shared-users", 1))).ToList();
    }

    public async Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken)
        => await Connected().ExecuteAsync("/ip/hotspot/user/add",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["password"] = password,
                ["profile"] = profile,
                ["limit-uptime"] = RouterOsTime.ToRouterOs(limit)
            }, cancellationToken);

    public async Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken)
        => await Connected().ExecuteAsync("/ip/hotspot/user/profile/add",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["rate-limit"] = rateLimit,
                ["shared-users"] = "1",
                ["comment"] = ProfileMarker.Build(ticketDuration)
            }, cancellationToken);

    /// <summary>
    /// Elimina por <c>.id</c>, que es lo que acepta <c>numbers</c>: pasar nombres fallaba contra
    /// un equipo real. Se lista una sola vez y se borra todo en una llamada con
    /// <c>numbers=id1,id2,...</c>. Los nombres que ya no existen se ignoran: el estado deseado
    /// (que no estén) ya se cumple. Devuelve cuántos se borraron.
    /// </summary>
    public async Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        if (names.Count == 0)
            return 0;

        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var session = Connected();
        var ids = (await session.ListAsync("/ip/hotspot/user/print", cancellationToken))
            .Where(r => wanted.Contains(r.GetString("name")))
            .Select(r => r.GetString(".id"))
            .ToList();

        if (ids.Count == 0)
            return 0;

        await session.ExecuteAsync("/ip/hotspot/user/remove",
            new Dictionary<string, string> { ["numbers"] = string.Join(",", ids) }, cancellationToken);
        return ids.Count;
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
    Task<IReadOnlyList<ITikRow>> ListAsync(string command, CancellationToken cancellationToken);
    Task ExecuteAsync(string command, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken);
}

public interface ITikSessionFactory
{
    Task<ITikSession> OpenAsync(RouterSettings settings, CancellationToken cancellationToken);
}

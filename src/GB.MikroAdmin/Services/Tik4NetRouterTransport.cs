using GB.MikroAdmin.Models;
namespace GB.MikroAdmin.Services;
/// <summary>Adaptador de tik4net. Las llamadas crudas se concentran aquí para no filtrar API RouterOS a la UI.</summary>
public sealed class Tik4NetRouterTransport : IRouterTransport
{
    dynamic? _connection;
    public async Task ConnectAsync(RouterSettings s, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.Host) || string.IsNullOrWhiteSpace(s.UserName)) throw new ArgumentException("Host y usuario son obligatorios.");
        if (!s.UseTls) throw new InvalidOperationException("El modo real exige API-SSL/TLS. Actívalo en el router y usa normalmente el puerto 8729.");
        // Valida la cadena, revocación y nombre/SAN antes de entregar la conexión a tik4net.
        await RouterCertificateValidator.ValidateAsync(s.Host, s.Port, ct);
        var factory = Type.GetType("tik4net.ConnectionFactory, tik4net", true)!;
        var kind = Type.GetType("tik4net.TikConnectionType, tik4net", true)!;
        _connection = factory.GetMethod("CreateConnection")!.Invoke(null, [Enum.Parse(kind, "ApiSsl")])!;
        _connection.Open(s.Host, s.Port, s.UserName, s.Password);
    }
    public Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken ct)
    {
        var rows = (System.Collections.IEnumerable)_connection!.CreateCommandAndParameters("/interface/print").ExecuteList(); var result = new List<RouterInterface>();
        foreach (dynamic r in rows) result.Add(new((string)(r.GetFieldValue("name") ?? ""), (string?)r.GetFieldValue("running") == "true", (string?)r.GetFieldValue("disabled") == "true", (string)(r.GetFieldValue("type") ?? ""))); return Task.FromResult<IReadOnlyList<RouterInterface>>(result);
    }
    public Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken ct)
    {
        var rows = (System.Collections.IEnumerable)_connection!.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList(); var result = new List<HotspotUser>();
        foreach (dynamic r in rows) { var limit = ParseTime((string?)r.GetFieldValue("limit-uptime")); var uptime = ParseTime((string?)r.GetFieldValue("uptime")) ?? TimeSpan.Zero; result.Add(new((string)(r.GetFieldValue("name") ?? ""), (string)(r.GetFieldValue("profile") ?? "default"), limit, uptime, uptime > TimeSpan.Zero)); }
        return Task.FromResult<IReadOnlyList<HotspotUser>>(result);
    }
    public Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken ct)
    {
        var rows = (System.Collections.IEnumerable)_connection!.CreateCommandAndParameters("/ip/hotspot/user/profile/print").ExecuteList(); var result = new List<HotspotProfile>();
        foreach (dynamic r in rows)
        {
            var comment = (string?)r.GetFieldValue("comment");
            result.Add(new(
                (string)(r.GetFieldValue("name") ?? ""),
                (string?)r.GetFieldValue("rate-limit"),
                ParsePlanDuration(comment),
                int.TryParse((string?)r.GetFieldValue("shared-users"), out var sharedUsers) ? sharedUsers : 1));
        }
        return Task.FromResult<IReadOnlyList<HotspotProfile>>(result);
    }
    public Task AddUserAsync(string n, string p, string profile, TimeSpan limit, CancellationToken ct) { _connection!.CreateCommandAndParameters("/ip/hotspot/user/add", "name", n, "password", p, "profile", profile, "limit-uptime", ToRouterOsTime(limit)).ExecuteNonQuery(); return Task.CompletedTask; }
    public Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken ct) { _connection!.CreateCommandAndParameters("/ip/hotspot/user/profile/add", "name", name, "rate-limit", rateLimit, "shared-users", "1", "comment", $"GBM:voucher-duration={ToRouterOsTime(voucherDuration)}").ExecuteNonQuery(); return Task.CompletedTask; }
    public Task RemoveUserAsync(string n, CancellationToken ct) { _connection!.CreateCommandAndParameters("/ip/hotspot/user/remove", "numbers", n).ExecuteNonQuery(); return Task.CompletedTask; }
    static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (TimeSpan.TryParse(value, out var timeSpan))
            return timeSpan;

        var total = TimeSpan.Zero;
        var index = 0;
        while (index < value.Length)
        {
            var start = index;
            while (index < value.Length && char.IsDigit(value[index]))
                index++;
            if (start == index || index >= value.Length || !int.TryParse(value[start..index], out var amount))
                return null;

            total += value[index++] switch
            {
                'w' => TimeSpan.FromDays(amount * 7),
                'd' => TimeSpan.FromDays(amount),
                'h' => TimeSpan.FromHours(amount),
                'm' => TimeSpan.FromMinutes(amount),
                's' => TimeSpan.FromSeconds(amount),
                _ => TimeSpan.Zero
            };
        }

        return total;
    }
    static string ToRouterOsTime(TimeSpan v) => $"{(int)v.TotalHours}h{v.Minutes}m";
    static TimeSpan? ParsePlanDuration(string? comment) { const string prefix = "GBM:voucher-duration="; return comment?.StartsWith(prefix, StringComparison.Ordinal) == true ? ParseTime(comment[prefix.Length..]) : null; }
    public ValueTask DisposeAsync() { if (_connection is not null) _connection.Close(); return ValueTask.CompletedTask; }
}

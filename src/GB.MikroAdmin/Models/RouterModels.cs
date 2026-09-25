namespace GB.MikroAdmin.Models;

public record RouterSettings(string Host, int Port, string UserName, string Password, bool UseTls, bool Simulated);
public record RouterInterface(string Name, bool Running, bool Disabled, string Type);
public record HotspotUser(string Name, string Profile, TimeSpan? LimitUptime, TimeSpan Uptime, bool Active)
{
    public TimeSpan? Remaining => LimitUptime is { } limit ? (limit > Uptime ? limit - Uptime : TimeSpan.Zero) : null;
    public bool IsExpired => LimitUptime is { } limit && Uptime >= limit;
}
public record HotspotProfile(
    string Name,
    string? RateLimit,
    TimeSpan? VoucherDuration,
    int SharedUsers);

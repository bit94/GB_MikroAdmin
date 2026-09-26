namespace GB.MikroAdmin.Core.Models;

public record RouterSettings(string Host, int Port, string UserName, string Password, bool UseTls, bool Simulated);

public record RouterInterface(string Name, bool Running, bool Disabled, string Type);

/// <summary>Estado de consumo de un voucher, derivado del tiempo consumido frente al límite.</summary>
public enum VoucherState
{
    /// <summary>Sin límite de tiempo configurado: el consumo no determina su estado.</summary>
    Unlimited,
    /// <summary>Aún no consumido y con tiempo restante.</summary>
    Available,
    /// <summary>Consumido parcialmente.</summary>
    InUse,
    /// <summary>Agotó su tiempo, pero el equipo aún lo conserva.</summary>
    Expired
}

public record HotspotUser(string Name, string Profile, TimeSpan? LimitUptime, TimeSpan Uptime, bool Active)
{
    public TimeSpan? Remaining => LimitUptime is { } limit ? (limit > Uptime ? limit - Uptime : TimeSpan.Zero) : null;

    public bool IsExpired => LimitUptime is { } limit && Uptime >= limit;

    /// <summary>Porcentaje consumido de 0 a 100; 0 cuando no hay límite.</summary>
    public double ConsumedPercent => LimitUptime is { Ticks: > 0 } limit
        ? Math.Clamp(Uptime.TotalMilliseconds / limit.TotalMilliseconds * 100, 0, 100)
        : 0;

    public VoucherState State => LimitUptime is null
        ? VoucherState.Unlimited
        : IsExpired ? VoucherState.Expired
        : Uptime > TimeSpan.Zero ? VoucherState.InUse
        : VoucherState.Available;
}

public record HotspotProfile(
    string Name,
    string? RateLimit,
    TimeSpan? VoucherDuration,
    int SharedUsers);

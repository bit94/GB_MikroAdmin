namespace GB.MikroAdmin.Core.Models;

public record RouterSettings(string Host, int Port, string UserName, string Password, bool UseTls, bool Simulated);

public record RouterInterface(string Name, bool Running, bool Disabled, string Type);

/// <summary>Estado de consumo de un ticket, derivado del tiempo consumido y de si tiene sesión activa.</summary>
public enum TicketState
{
    /// <summary>Sin límite de tiempo configurado: el consumo no determina su estado.</summary>
    Unlimited,
    /// <summary>Aún no consumido y con tiempo restante.</summary>
    Available,
    /// <summary>Tiene una sesión activa en este momento.</summary>
    Online,
    /// <summary>Ya consumió tiempo y no tiene sesión activa: el contador está en pausa.</summary>
    Paused,
    /// <summary>Agotó su tiempo, pero el equipo aún lo conserva.</summary>
    Expired
}

public record HotspotUser(
    string Name,
    string Profile,
    TimeSpan? LimitUptime,
    TimeSpan Uptime,
    bool IsOnline)
{
    public TimeSpan? Remaining => LimitUptime is { } limit ? (limit > Uptime ? limit - Uptime : TimeSpan.Zero) : null;

    public bool IsExpired => LimitUptime is { } limit && Uptime >= limit;

    /// <summary>Porcentaje consumido de 0 a 100; 0 cuando no hay límite.</summary>
    public double ConsumedPercent => LimitUptime is { Ticks: > 0 } limit
        ? Math.Clamp(Uptime.TotalMilliseconds / limit.TotalMilliseconds * 100, 0, 100)
        : 0;

    public TicketState State => LimitUptime is null
        ? TicketState.Unlimited
        : IsExpired ? TicketState.Expired
        : IsOnline ? TicketState.Online
        : Uptime > TimeSpan.Zero ? TicketState.Paused
        : TicketState.Available;
}

public record HotspotProfile(
    string Name,
    string? RateLimit,
    TimeSpan? TicketDuration,
    int SharedUsers);

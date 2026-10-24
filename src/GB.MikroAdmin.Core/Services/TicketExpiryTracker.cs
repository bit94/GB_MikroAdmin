using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Recuerda qué tickets ya se avisaron como agotados. El router no envía eventos: el consumo
/// solo se conoce al consultar, así que hay que evitar avisar del mismo ticket en cada consulta.
/// </summary>
public sealed class TicketExpiryTracker
{
    private readonly HashSet<string> _knownExpired = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Devuelve los tickets agotados que aún no se habían avisado. Si un ticket se elimina del
    /// router se deja de recordar, de modo que un código reutilizado vuelve a avisar.
    /// </summary>
    public IReadOnlyList<HotspotUser> Observe(IReadOnlyList<HotspotUser> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        var present = users.Select(user => user.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _knownExpired.IntersectWith(present);

        var newlyExpired = new List<HotspotUser>();
        foreach (var user in users)
        {
            if (user.State == TicketState.Expired && _knownExpired.Add(user.Name))
                newlyExpired.Add(user);
        }

        return newlyExpired;
    }

    public void Reset() => _knownExpired.Clear();
}

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// RouterOS no tiene un campo de "duración del ticket" en los perfiles de Hotspot, así que
/// GB.MikroAdmin la guarda en el comentario del perfil con este marcador.
/// </summary>
public static class ProfileMarker
{
    // El valor persiste en comentarios de perfiles ya creados en routers reales:
    // aunque la app ya hable de "tickets", este formato no debe cambiar nunca.
    public const string Prefix = "GBM:voucher-duration=";

    public static string Build(TimeSpan ticketDuration) => Prefix + RouterOsTime.ToRouterOs(ticketDuration);

    /// <summary>Devuelve la duración marcada, o <c>null</c> si el comentario no la declara.</summary>
    public static TimeSpan? ReadTicketDuration(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        // El marcador puede ir tras otras notas del administrador; se toma el primer token tras él.
        var start = comment.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        var payload = comment[(start + Prefix.Length)..].Trim();
        var separator = payload.IndexOf(' ');
        if (separator >= 0)
            payload = payload[..separator];

        return RouterOsTime.Parse(payload);
    }
}

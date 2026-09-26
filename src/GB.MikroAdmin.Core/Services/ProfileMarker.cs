namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// RouterOS no tiene un campo de "duración del voucher" en los perfiles de Hotspot, así que
/// GB.MikroAdmin la guarda en el comentario del perfil con este marcador.
/// </summary>
public static class ProfileMarker
{
    public const string Prefix = "GBM:voucher-duration=";

    public static string Build(TimeSpan voucherDuration) => Prefix + RouterOsTime.ToRouterOs(voucherDuration);

    /// <summary>Devuelve la duración marcada, o <c>null</c> si el comentario no la declara.</summary>
    public static TimeSpan? ReadVoucherDuration(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment) || !comment.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        // Un comentario puede contener otras notas tras el marcador; se toma el primer token.
        var payload = comment[Prefix.Length..].Trim();
        var separator = payload.IndexOf(' ');
        if (separator >= 0)
            payload = payload[..separator];

        return RouterOsTime.Parse(payload);
    }
}

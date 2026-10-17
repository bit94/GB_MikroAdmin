using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

// El escapado CSV es un detalle de implementación, no API pública: se expone solo a las pruebas.
[assembly: InternalsVisibleTo("GB.MikroAdmin.Tests")]

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Convierte un lote de tickets en texto exportable. Vive en Core, sin dependencias de
/// plataforma, para poder verificar el formato con pruebas.
/// </summary>
public static class TicketExporter
{
    /// <summary>
    /// CSV con encabezado. Se separa con punto y coma y no con coma: el destino habitual es
    /// Excel en configuración regional española, donde la coma es el separador decimal y
    /// partiría las columnas mal.
    /// </summary>
    public static string ToCsv(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();
        sb.AppendLine("Codigo;Usuario;Contrasena;Perfil;Duracion;DuracionTexto;Creado");

        foreach (var ticket in batch.Tickets)
        {
            sb.Append(Escape(ticket.Name)).Append(';')
              .Append(Escape(ticket.Password)).Append(';')
              .Append(Escape(ticket.Password)).Append(';')
              .Append(Escape(batch.Profile)).Append(';')
              .Append(Escape(batch.Duration.ToString(@"d\.hh\:mm\:ss"))).Append(';')
              .Append(Escape(batch.DurationText)).Append(';')
              .Append(Escape(batch.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
              .AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Lista de códigos separados por comas, para pegar en un mensaje.</summary>
    public static string ToPlainList(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();
        sb.AppendLine($"Tickets · {batch.Tickets.Count} · {batch.Profile} · {batch.DurationText}");

        foreach (var ticket in batch.Tickets)
            sb.AppendLine(ticket.Name);

        return sb.ToString();
    }

    /// <summary>
    /// Escapa un campo CSV según RFC 4180: si contiene el separador, comillas o un salto de
    /// línea, se encierra entre comillas y las comillas internas se duplican.
    /// </summary>
    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}

/// <summary>
/// Datos de un lote listos para exportar. Se separa del modelo de la interfaz para que el
/// exportador no dependa de nada de MAUI.
/// </summary>
/// <param name="Brand">Nombre comercial que encabeza la tarjeta impresa; null lo omite.</param>
/// <param name="Ssid">Red Wi-Fi que se indica en la tarjeta; null lo omite.</param>
/// <param name="Price">Precio de venta del ticket; null lo omite.</param>
/// <param name="StatusUrl">Portal donde el cliente consulta su estado; null lo omite.</param>
public sealed record TicketBatchExport(
    DateTimeOffset CreatedAt,
    string Profile,
    TimeSpan Duration,
    IReadOnlyList<CreatedHotspotUser> Tickets,
    string? Brand = null,
    string? Ssid = null,
    decimal? Price = null,
    string? StatusUrl = null)
{
    /// <summary>Códigos del lote que ya se enviaron a imprimir. Los lotes guardados antes de
    /// existir este seguimiento carecen de la lista y se deserializan con la colección vacía.</summary>
    public IList<string> Printed { get; init; } = [];

    public string DurationText => Humanize(Duration);

    /// <summary>
    /// Qué se codifica en el QR de un ticket. Con red Wi-Fi, el payload de conexión automática
    /// <c>WIFI:T:WPA;S:…;P:…;;</c>, que los móviles interpretan como «conectarse a esta red»;
    /// sin red, la URL de estado con el código incrustado si se configuró; en su defecto, el
    /// propio código, que al ser a la vez usuario y contraseña basta para entrar.
    /// </summary>
    public string QrPayloadFor(CreatedHotspotUser ticket)
    {
        if (Ssid?.Trim() is { Length: > 0 } ssid)
            return $"WIFI:T:WPA;S:{WifiEscape(ssid)};P:{WifiEscape(ticket.Password)};;";

        if (StatusUrl?.Trim() is { Length: > 0 } url)
            return ComposeStatusUrl(url, ticket.Name);

        return ticket.Name;
    }

    private static string ComposeStatusUrl(string template, string code)
    {
        var encoded = Uri.EscapeDataString(code);
        if (template.Contains("{codigo}", StringComparison.OrdinalIgnoreCase))
            return template.Replace("{codigo}", encoded, StringComparison.OrdinalIgnoreCase);
        return $"{template.TrimEnd('/')}/{encoded}";
    }

    /// <summary>El formato WIFI: exige anteponer barra invertida a \, ;, , y : dentro de los campos.</summary>
    private static string WifiEscape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:");

    /// <summary>Duración en lenguaje corriente: "1 h" se lee mejor que "01:00:00".</summary>
    public static string Humanize(TimeSpan value)
    {
        if (value.TotalDays >= 1 && value.TotalDays == Math.Floor(value.TotalDays))
            return $"{(int)value.TotalDays} d";
        if (value.TotalHours >= 1 && value.Minutes == 0 && value.Seconds == 0)
            return $"{(int)value.TotalHours} h";
        if (value.TotalMinutes >= 1 && value.Seconds == 0)
            return $"{(int)value.TotalMinutes} min";
        return value.ToString(@"hh\:mm\:ss");
    }
}

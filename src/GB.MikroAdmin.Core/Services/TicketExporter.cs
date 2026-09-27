using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

// El escapado CSV es un detalle de implementación, no API pública: se expone solo a las pruebas.
[assembly: InternalsVisibleTo("GB.MikroAdmin.Tests")]

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Convierte un lote de vouchers en texto exportable. Vive en Core, sin dependencias de
/// plataforma, para poder verificar el formato con pruebas.
/// </summary>
public static class VoucherExporter
{
    /// <summary>
    /// CSV con encabezado. Se separa con punto y coma y no con coma: el destino habitual es
    /// Excel en configuración regional española, donde la coma es el separador decimal y
    /// partiría las columnas mal.
    /// </summary>
    public static string ToCsv(VoucherBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();
        sb.AppendLine("Codigo;Usuario;Contrasena;Perfil;Duracion;DuracionTexto;Creado");

        foreach (var voucher in batch.Vouchers)
        {
            sb.Append(Escape(voucher.Name)).Append(';')
              .Append(Escape(voucher.Password)).Append(';')
              .Append(Escape(voucher.Password)).Append(';')
              .Append(Escape(batch.Profile)).Append(';')
              .Append(Escape(batch.Duration.ToString(@"d\.hh\:mm\:ss"))).Append(';')
              .Append(Escape(batch.DurationText)).Append(';')
              .Append(Escape(batch.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
              .AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Lista de códigos separados por comas, para pegar en un mensaje.</summary>
    public static string ToPlainList(VoucherBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();
        sb.AppendLine($"Vouchers · {batch.Vouchers.Count} · {batch.Profile} · {batch.DurationText}");

        foreach (var voucher in batch.Vouchers)
            sb.AppendLine(voucher.Name);

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
public sealed record VoucherBatchExport(
    DateTimeOffset CreatedAt,
    string Profile,
    TimeSpan Duration,
    IReadOnlyList<CreatedHotspotUser> Vouchers)
{
    public string DurationText => Humanize(Duration);

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

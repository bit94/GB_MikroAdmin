using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Convierte un lote de tickets en un documento HTML autocontenido: estilos incrustados y QR
/// como PNG en base64, sin recursos externos. El archivo se puede editar a mano —cambiar
/// colores, tipografía, tamaños o quitar líneas— y reimprimir desde el navegador, que pagina
/// el formato carta mejor que cualquier visor de PDF improvisado.
/// </summary>
public static class TicketHtmlWriter
{
    public static string ToHtml(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"es\">\n<head>\n<meta charset=\"utf-8\">\n")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
          .Append("<title>Tickets ").Append(H(batch.Profile)).Append("</title>\n")
          .Append("""
            <style>
              @page { size: letter; margin: 10mm; }
              * { box-sizing: border-box; }
              body { font-family: Arial, Helvetica, sans-serif; margin: 0; color: #111; }
              header { font-size: 9pt; color: #444; border-bottom: 0.4pt solid #aaa;
                       padding-bottom: 2mm; margin-bottom: 3mm; }
              .grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 2.5mm; }
              .card { border: 0.3mm dashed #888; padding: 2mm; text-align: center;
                      break-inside: avoid; page-break-inside: avoid; position: relative; }
              .num { position: absolute; top: 1mm; right: 1.5mm; font-size: 5.5pt; color: #777; }
              .ssid { font-weight: bold; font-size: 8pt; margin-bottom: 1mm; }
              .code { font-family: "Courier New", monospace; font-weight: bold;
                      font-size: 13pt; letter-spacing: 0.5pt; margin-bottom: 1.5mm; }
              .qr img { width: 24mm; height: 24mm; }
              .details { font-size: 6.5pt; font-weight: bold; margin-top: 1mm; }
              .meta { font-size: 5.5pt; color: #555; margin-top: 0.5mm; }
            </style>
            </head>
            <body>
            """);

        sb.Append("\n<header>").Append(H(Header(batch))).Append("</header>\n<div class=\"grid\">\n");

        for (var index = 0; index < batch.Tickets.Count; index++)
        {
            var ticket = batch.Tickets[index];
            sb.Append("<div class=\"card\"><span class=\"num\">#").Append(index + 1).Append("</span>");

            if (batch.Ssid?.Trim() is { Length: > 0 } ssid)
                sb.Append("<div class=\"ssid\">Red: ").Append(H(ssid)).Append("</div>");
            else if (batch.Brand?.Trim() is { Length: > 0 } brand)
                sb.Append("<div class=\"ssid\">").Append(H(brand)).Append("</div>");

            sb.Append("<div class=\"code\">").Append(H(ticket.Name)).Append("</div>")
              .Append("<div class=\"qr\"><img src=\"data:image/png;base64,")
              .Append(Convert.ToBase64String(QrPng(batch.QrPayloadFor(ticket))))
              .Append("\" alt=\"QR\"></div>");

            sb.Append("<div class=\"details\">").Append(H(Details(batch))).Append("</div>");

            if (batch.StatusUrl?.Trim() is { Length: > 0 } statusUrl)
                sb.Append("<div class=\"meta\">Estado: ").Append(H(statusUrl)).Append("</div>");

            sb.Append("</div>\n");
        }

        sb.Append("</div>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static string Header(TicketBatchExport batch)
    {
        var header = $"{batch.Brand?.Trim().TrimEnd('.') ?? "MikroAdmin"} · {batch.Tickets.Count} tickets · " +
                     $"{batch.Profile} · {batch.DurationText} · {batch.CreatedAt:dd/MM/yyyy HH:mm}";
        return header;
    }

    /// <summary>Precio y duración bajo el QR; el cliente ve ambos datos al escanear la tarjeta.</summary>
    private static string Details(TicketBatchExport batch)
    {
        var duration = "Duración: " + batch.DurationText;
        return batch.Price is { } price
            ? "$ " + price.ToString("0.00", CultureInfo.InvariantCulture) + " · " + duration
            : duration;
    }

    private static byte[] QrPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(4);
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
}

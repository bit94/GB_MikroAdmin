using System.Globalization;
using System.Text;
using QRCoder;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Genera un PDF con los tickets en tarjetas recortables o en un ticket individual para rollo
/// térmico de 80 mm.
/// </summary>
/// <remarks>
/// El PDF se escribe a mano en lugar de añadir una librería PDF: el documento es una retícula de
/// tarjetas con texto y un QR, y las librerías habituales añaden varios megabytes a una
/// aplicación móvil para un caso que aquí se resuelve con unas decenas de operadores de dibujo.
/// Las fuentes son Helvetica y Helvetica-Bold, dos de las catorce estándar que todo lector
/// incorpora, así que no hay que empotrar ningún archivo de fuente. La única dependencia es
/// QRCoder, que calcula la matriz del QR: dibujarla como cuadrados negros es trivial una vez
/// conocida.
/// </remarks>
public static class TicketPdfWriter
{
    // Medidas en puntos PDF: una hoja carta son 612 x 792 y un rollo de 80 mm, 226,8 de ancho.
    private const double SheetWidth = 612;
    private const double SheetHeight = 792;
    private const double ThermalWidth = 226.8;
    private const double Margin = 18;
    private const int Columns = 5;
    private const int Rows = 6;
    private const double Gap = 6;

    /// <summary>Estructura fija de objetos antes de las páginas: catálogo, árbol y fuentes.</summary>
    private const int CatalogId = 1;
    private const int PagesId = 2;
    private const int RegularFontId = 3;
    private const int BoldFontId = 4;
    private const int FirstPageId = 5;

    /// <summary>Tarjetas recortables con toda la hoja aprovechada: 5 × 6 por cara de carta.</summary>
    public static byte[] WriteSheet(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var cardWidth = (SheetWidth - (2 * Margin) - ((Columns - 1) * Gap)) / Columns;
        var cardHeight = (SheetHeight - (2 * Margin) - HeaderHeight - ((Rows - 1) * Gap)) / Rows;
        var perPage = Columns * Rows;
        var pages = Math.Max(1, (int)Math.Ceiling(batch.Tickets.Count / (double)perPage));

        var objects = BaseObjects();

        for (var page = 0; page < pages; page++)
        {
            var slice = batch.Tickets.Skip(page * perPage).Take(perPage).ToList();
            var content = RenderPage(slice, page, pages, batch, cardWidth, cardHeight);
            AddPage(objects, SheetWidth, SheetHeight, content);
        }

        FinishPages(objects, pages);
        return BuildFile(objects);
    }

    /// <summary>
    /// Un solo ticket en un PDF de 80 mm de ancho, para impresora térmica de rollo. La página
    /// mide lo que mide el contenido: en un rollo, cada punto de altura extra es papel que se
    /// desecha.
    /// </summary>
    public static byte[] WriteThermal(TicketBatchExport batch, CreatedHotspotUser ticket)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(ticket);

        var objects = BaseObjects();
        var content = new StringBuilder();

        DrawCard(content, 0, 0, ThermalWidth, ThermalCardHeight(batch), ticket, batch, thermal: true);
        AddPage(objects, ThermalWidth, ThermalCardHeight(batch), content.ToString());

        FinishPages(objects, pages: 1);
        return BuildFile(objects);
    }

    /// <summary>Altura mínima de la banda de encabezado de la hoja: texto más regla.</summary>
    private const double HeaderHeight = 22;

    private static List<string> BaseObjects() =>
    [
        // 1: catálogo
        $"<< /Type /Catalog /Pages {PagesId} 0 R >>",
        // 2: árbol de páginas, rellenado al final con los identificadores de cada página
        string.Empty,
        // 3 y 4: Helvetica y Helvetica-Bold, dos de las fuentes estándar del formato
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
    ];

    private static void AddPage(List<string> objects, double width, double height, string content)
    {
        // Cada página ocupa dos objetos: la página y su flujo de contenido.
        var pageId = objects.Count + 1;
        var contentId = pageId + 1;

        objects.Add($"<< /Type /Page /Parent {PagesId} 0 R /MediaBox [0 0 {N(width)} {N(height)}] " +
                    $"/Resources << /Font << /F1 {RegularFontId} 0 R /F2 {BoldFontId} 0 R >> >> " +
                    $"/Contents {contentId} 0 R >>");
        objects.Add(Stream(content));
    }

    private static void FinishPages(List<string> objects, int pages)
    {
        var pageIds = string.Join(" ",
            Enumerable.Range(0, pages).Select(p => $"{FirstPageId + (p * 2)} 0 R"));
        objects[PagesId - 1] = $"<< /Type /Pages /Kids [{pageIds}] /Count {pages} >>";
    }

    private static string RenderPage(
        IReadOnlyList<CreatedHotspotUser> tickets,
        int pageIndex,
        int totalPages,
        TicketBatchExport batch,
        double cardWidth,
        double cardHeight)
    {
        var sb = new StringBuilder();

        // Encabezado: da contexto a la hoja cuando se separa del resto del lote.
        var header = $"{batch.Brand?.Trim().TrimEnd('.') ?? "MikroAdmin"} · {batch.Tickets.Count} tickets · " +
                     $"{batch.Profile} · {batch.DurationText} · {batch.CreatedAt:dd/MM/yyyy HH:mm}";
        if (totalPages > 1)
            header += $" · página {pageIndex + 1} de {totalPages}";
        Text(sb, Margin, SheetHeight - Margin, 8, header);
        Line(sb, Margin, SheetHeight - Margin - 4, SheetWidth - Margin, SheetHeight - Margin - 4);

        for (var index = 0; index < tickets.Count; index++)
        {
            var column = index % Columns;
            var row = index / Columns;

            var x = Margin + (column * (cardWidth + Gap));
            // Se dibuja de arriba hacia abajo: el índice 0 es la tarjeta superior izquierda.
            var y = SheetHeight - Margin - HeaderHeight - ((row + 1) * cardHeight) - (row * Gap);

            DrawCard(sb, x, y, cardWidth, cardHeight, tickets[index], batch, thermal: false, number: index + 1);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Altura exacta de la tarjeta térmica: la página del PDF adopta esta medida. Debe mantenerse
    /// en sync con <see cref="DrawCard"/>: es la misma lista de líneas y sus saltos de cursor.
    /// </summary>
    private static double ThermalCardHeight(TicketBatchExport batch)
    {
        const double Padding = 10;
        var height = Padding;

        if (HasTitle(batch))
            height += 11;                    // red (o marca)
        height += 28;                        // código
        height += ThermalQrSize(batch) + 4;  // QR
        height += 16;                        // precio y duración
        return height + Padding;
    }

    /// <summary>La tarjeta concentra lo que el cliente necesita: red, clave, QR, precio y duración.</summary>
    private static bool HasTitle(TicketBatchExport batch) =>
        !string.IsNullOrWhiteSpace(batch.Ssid) || !string.IsNullOrWhiteSpace(batch.Brand);

    /// <summary>Primera línea centrada en negrita: la red Wi-Fi manda porque es lo que se conecta;
    /// sin ella, la marca identifica el negocio.</summary>
    private static string? TitleFor(TicketBatchExport batch)
    {
        if (batch.Ssid?.Trim() is { Length: > 0 } ssid)
            return $"Red: {ssid}";
        if (batch.Brand?.Trim() is { Length: > 0 } brand)
            return brand;
        return null;
    }

    private static void DrawCard(
        StringBuilder sb,
        double x,
        double y,
        double width,
        double height,
        CreatedHotspotUser ticket,
        TicketBatchExport batch,
        bool thermal,
        int number = 0)
    {
        // Marco gris claro solo en la hoja: guía de recorte sin gastar tinta. En térmica el borde
        // del papel ya separa el ticket y el marco sería tinta gastada en un recibo desechable.
        if (!thermal)
        {
            sb.AppendLine("0.70 0.74 0.78 RG 0.5 w");
            sb.AppendLine($"{N(x)} {N(y)} {N(width)} {N(height)} re S");
        }

        var padding = thermal ? 10.0 : 6.0;
        var inner = width - (2 * padding);
        // Las líneas se apilan desde arriba; el cursor baja a medida que se dibuja.
        var cursor = y + height - padding;

        if (number > 0)
            Text(sb, x + padding, cursor - 5.5, 5.5, $"#{number}", alignRightX: x + width - padding);

        if (TitleFor(batch) is { } title)
        {
            var titleSize = FittedSize(title, inner, thermal ? 9 : 7.5, 5.5);
            Text(sb, x + padding, cursor - titleSize, titleSize, title, bold: true, centerX: x + width / 2);
            cursor -= thermal ? 11 : 9;
        }

        // El código es lo que se dicta y se teclea: centrado y en negrita.
        var codeSize = FittedSize(ticket.Name, inner, thermal ? 24 : 16, thermal ? 13 : 10);
        Text(sb, x + padding, cursor - codeSize, codeSize, ticket.Name, bold: true, centerX: x + width / 2);
        cursor -= thermal ? 28 : 19;

        var qrSize = thermal ? ThermalQrSize(batch) : Math.Min(64, inner);
        Qr(sb, x + (width - qrSize) / 2, cursor - qrSize, qrSize, batch.QrPayloadFor(ticket));
        cursor -= qrSize + (thermal ? 4 : 0);

        var details = Details(batch);
        var detailsSize = thermal ? 10.5 : FittedSize(details, inner, 7.5, 5.5);
        Text(sb, x + padding, cursor - detailsSize, detailsSize, details, bold: true, centerX: x + width / 2);
    }

    /// <summary>Precio y duración bajo el QR; el precio se omite en lotes antiguos sin ese dato.</summary>
    private static string Details(TicketBatchExport batch)
    {
        var duration = "Duración: " + batch.DurationText;
        return batch.Price is { } price
            ? "$ " + price.ToString("0.00", CultureInfo.InvariantCulture) + " · " + duration
            : duration;
    }

    private static double ThermalQrSize(TicketBatchExport batch)
    {
        const double ThermalPadding = 10;
        var inner = ThermalWidth - (2 * ThermalPadding);
        return Math.Min(140, inner * 0.68);
    }

    /// <summary>
    /// Dibuja la matriz del QR como rectángulos negros. El margen de silencio no se pinta: el
    /// fondo blanco de la tarjeta ya lo aporta, y así los módulos negros ocupan todo el espacio
    /// asignado en vez de encogerse un 30% para dejar sitio a un margen blanco redundante.
    /// </summary>
    private static void Qr(StringBuilder sb, double x, double y, double size, string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        var modules = matrix.Count;
        var moduleSize = size / modules;

        sb.AppendLine("0 0 0 rg");
        for (var row = 0; row < modules; row++)
        {
            var bits = matrix[row];
            var runStart = -1;
            for (var column = 0; column <= modules; column++)
            {
                var dark = column < modules && bits[column];
                if (dark && runStart < 0)
                    runStart = column;
                else if (!dark && runStart >= 0)
                {
                    EmitRun(sb, x, y, row, runStart, column - runStart, moduleSize, modules);
                    runStart = -1;
                }
            }
        }
    }

    private static void EmitRun(
        StringBuilder sb, double x, double y, int row, int start, int length,
        double moduleSize, int modules)
    {
        var runX = x + (start * moduleSize);
        // La fila 0 de la matriz es la superior; el eje Y del PDF crece hacia arriba.
        var runY = y + ((modules - row - 1) * moduleSize);
        sb.AppendLine($"{N(runX)} {N(runY)} {N(length * moduleSize)} {N(moduleSize)} re f");
    }

    /// <summary>Reduce el tamaño de la fuente hasta que el texto quepa en el ancho disponible.</summary>
    private static double FittedSize(string value, double availableWidth, double preferred, double minimum)
    {
        // Los dígitos de Helvetica miden aproximadamente 0.556 em, que es el caso habitual aquí.
        const double AverageGlyphWidth = 0.58;
        var size = preferred;
        while (size > minimum && value.Length * size * AverageGlyphWidth > availableWidth)
            size -= 0.5;
        return size;
    }

    private static void Text(
        StringBuilder sb, double x, double y, double size, string value,
        bool bold = false, double? centerX = null, double? alignRightX = null)
    {
        // Centrado y alineado a la derecha con el ancho medio de Helvetica: una aproximación
        // suficiente para títulos cortos, donde un desajuste de un punto no se percibe.
        if (centerX is { } center)
            x = center - (value.Length * size * 0.52 / 2);
        else if (alignRightX is { } right)
            x = right - (value.Length * size * 0.52);

        sb.Append("BT 0 0 0 rg /").Append(bold ? "F2 " : "F1 ").Append(N(size)).Append(" Tf ")
          .Append(N(x)).Append(' ').Append(N(y)).Append(" Td (")
          .Append(Sanitize(value)).AppendLine(") Tj ET");
    }

    private static void Line(StringBuilder sb, double x1, double y1, double x2, double y2)
    {
        sb.AppendLine("0.70 0.74 0.78 RG 0.5 w");
        sb.AppendLine($"{N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S");
    }

    /// <summary>
    /// Helvetica con codificación WinAnsi solo cubre hasta U+00FF: se sustituye el resto para no
    /// emitir bytes que el lector interpretaría como otra cosa.
    /// </summary>
    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                default: sb.Append(c is >= ' ' and <= 'ÿ' ? c : '?'); break;
            }
        }
        return sb.ToString();
    }

    private static string Stream(string content)
    {
        var length = Encoding.Latin1.GetByteCount(content);
        return $"<< /Length {length} >>\nstream\n{content}\nendstream";
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Ensambla el archivo con su tabla de referencias cruzadas. Cada entrada del índice apunta al
    /// desplazamiento en bytes donde empieza el objeto, que es lo que permite al lector saltar
    /// directamente a cada uno.
    /// </summary>
    private static byte[] BuildFile(List<string> objects)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new List<int>(objects.Count);
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefPosition = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            sb.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");

        sb.Append("trailer\n<< /Size ").Append(objects.Count + 1)
          .Append(" /Root ").Append(CatalogId).Append(" 0 R >>\nstartxref\n")
          .Append(xrefPosition).Append("\n%%EOF\n");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}

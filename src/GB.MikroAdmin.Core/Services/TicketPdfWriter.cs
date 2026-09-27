using System.Globalization;
using System.Text;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Genera un PDF con los vouchers en formato de tarjetas recortables.
/// </summary>
/// <remarks>
/// El PDF se escribe a mano en lugar de añadir una dependencia: el documento es una retícula de
/// tarjetas con texto, y las librerías habituales añaden varios megabytes a una aplicación móvil
/// para un caso que aquí se resuelve con unas decenas de operadores de dibujo. La fuente es
/// Helvetica, una de las catorce estándar que todo lector incorpora, así que no hay que empotrar
/// ningún archivo de fuente.
/// </remarks>
public static class VoucherPdfWriter
{
    // Medidas en puntos PDF: una hoja A4 son 595 x 842.
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 30;
    private const int Columns = 3;
    private const int Rows = 9;
    private const double Gap = 8;

    /// <summary>Estructura fija de objetos antes de las páginas: catálogo, árbol y fuente.</summary>
    private const int CatalogId = 1;
    private const int PagesId = 2;
    private const int FontId = 3;

    public static byte[] Write(VoucherBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var cardWidth = (PageWidth - (2 * Margin) - ((Columns - 1) * Gap)) / Columns;
        var cardHeight = (PageHeight - (2 * Margin) - ((Rows - 1) * Gap)) / Rows;
        var perPage = Columns * Rows;
        var pages = Math.Max(1, (int)Math.Ceiling(batch.Vouchers.Count / (double)perPage));

        var objects = new List<string>
        {
            // 1: catálogo
            $"<< /Type /Catalog /Pages {PagesId} 0 R >>",
            // 2: árbol de páginas, rellenado tras conocer cuántas hay
            string.Empty,
            // 3: fuente Helvetica, una de las estándar del formato
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
        };

        for (var page = 0; page < pages; page++)
        {
            var slice = batch.Vouchers.Skip(page * perPage).Take(perPage).ToList();
            var content = RenderPage(slice, page, pages, batch, cardWidth, cardHeight);

            // Cada página ocupa dos objetos: la página y su flujo de contenido.
            var pageId = objects.Count + 1;
            var contentId = pageId + 1;

            objects.Add($"<< /Type /Page /Parent {PagesId} 0 R /MediaBox [0 0 {N(PageWidth)} {N(PageHeight)}] " +
                        $"/Resources << /Font << /F1 {FontId} 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add(Stream(content));
        }

        var pageIds = string.Join(" ", Enumerable.Range(0, pages).Select(p => 4 + (p * 2)));
        objects[PagesId - 1] = $"<< /Type /Pages /Kids [{pageIds}] /Count {pages} >>";

        return BuildFile(objects);
    }

    private static string RenderPage(
        IReadOnlyList<CreatedHotspotUser> vouchers,
        int pageIndex,
        int totalPages,
        VoucherBatchExport batch,
        double cardWidth,
        double cardHeight)
    {
        var sb = new StringBuilder();

        // Encabezado: da contexto a la hoja cuando se separa del resto del lote.
        var header = $"MikroAdmin · {batch.Vouchers.Count} vouchers · {batch.Profile} · " +
                     $"{batch.DurationText} · {batch.CreatedAt:dd/MM/yyyy HH:mm}";
        if (totalPages > 1)
            header += $" · página {pageIndex + 1} de {totalPages}";
        Text(sb, Margin, PageHeight - Margin, 8, header);
        Line(sb, Margin, PageHeight - Margin - 4, PageWidth - Margin, PageHeight - Margin - 4);

        for (var index = 0; index < vouchers.Count; index++)
        {
            var column = index % Columns;
            var row = index / Columns;

            var x = Margin + (column * (cardWidth + Gap));
            // Se dibuja de arriba hacia abajo: el índice 0 es la tarjeta superior izquierda.
            var y = PageHeight - Margin - 12 - ((row + 1) * cardHeight) - (row * Gap);

            DrawCard(sb, x, y, cardWidth, cardHeight, vouchers[index], batch);
        }

        return sb.ToString();
    }

    private static void DrawCard(
        StringBuilder sb,
        double x,
        double y,
        double width,
        double height,
        CreatedHotspotUser voucher,
        VoucherBatchExport batch)
    {
        // Marco gris claro: sirve de guía de recorte sin gastar tinta.
        sb.AppendLine("0.70 0.74 0.78 RG 0.5 w");
        sb.AppendLine($"{N(x)} {N(y)} {N(width)} {N(height)} re S");

        const double Padding = 7;
        var inner = width - (2 * Padding);

        // El código es lo más grande de la tarjeta: es el dato que se dicta y se teclea.
        var codeSize = FittedSize(voucher.Name, inner, 26, 12);
        Text(sb, x + Padding, y + height - Padding - codeSize, codeSize, voucher.Name);

        Text(sb, x + Padding, y + height - Padding - codeSize - 11, 7,
            $"{batch.Profile} · {batch.DurationText}");
        Text(sb, x + Padding, y + Padding + 6, 6.5, "usuario y contraseña");
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

    private static void Text(StringBuilder sb, double x, double y, double size, string value)
    {
        sb.Append("BT 0 0 0 rg /F1 ").Append(N(size)).Append(" Tf ")
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

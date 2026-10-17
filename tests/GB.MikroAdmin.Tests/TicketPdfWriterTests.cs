using System.Text;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

/// <summary>
/// Comprueba la estructura del PDF sin depender de una librería lectora: se verifican las
/// marcas que todo lector exige y los desplazamientos de la tabla de referencias cruzadas, que
/// es la parte que más fácilmente se rompe al escribir el archivo a mano.
/// </summary>
public sealed class TicketPdfWriterTests
{
    private static TicketBatchExport Batch(int count, string profile = "limitado", int hours = 2) => new(
        new DateTimeOffset(2026, 9, 26, 14, 30, 0, TimeSpan.Zero),
        profile,
        TimeSpan.FromHours(hours),
        Enumerable.Range(0, count)
            .Select(i => new CreatedHotspotUser((100_000 + i).ToString(), (100_000 + i).ToString()))
            .ToList());

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void Pdf_starts_with_the_signature_and_ends_with_the_marker()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(5)));

        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.EndsWith("%%EOF\n", pdf);
    }

    [Fact]
    public void Pdf_declares_a_catalog_and_a_page_tree()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(5)));

        Assert.Contains("/Type /Catalog", pdf);
        Assert.Contains("/Type /Pages", pdf);
        Assert.Contains("/Root 1 0 R", pdf);
    }

    [Fact]
    public void Pdf_embeds_no_font_file_and_uses_a_standard_one()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(5)));

        Assert.Contains("/BaseFont /Helvetica", pdf);
        // Helvetica es estándar: incrustarla engordaría el archivo sin necesidad.
        Assert.DoesNotContain("/FontFile", pdf);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(30, 1)]   // capacidad de una hoja carta (5 × 6)
    [InlineData(31, 2)]   // un ticket más y salta de página
    [InlineData(60, 2)]
    [InlineData(61, 3)]
    public void Pdf_paginates_at_the_cards_per_page(int tickets, int expectedPages)
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(tickets)));

        Assert.Contains($"/Count {expectedPages}", pdf);
        Assert.Equal(expectedPages, CountOccurrences(pdf, "/Type /Page "));
    }

    [Fact]
    public void Pdf_includes_every_code()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(20)));

        for (var i = 0; i < 20; i++)
            Assert.Contains($"({100_000 + i}) Tj", pdf);
    }

    /// <summary>
    /// Las entradas del índice deben apuntar al byte donde empieza cada objeto. Si no coinciden,
    /// el lector enseña el documento en blanco o lo rechaza directamente.
    /// </summary>
    [Fact]
    public void Pdf_cross_reference_offsets_point_at_the_objects()
    {
        var pdf = TicketPdfWriter.WriteSheet(Batch(30))
            .ToList()
            .ToArray();
        var text = Text(pdf);

        var startXref = text.LastIndexOf("startxref", StringComparison.Ordinal);
        var xrefOffset = int.Parse(text[(startXref + 9)..].Trim().Split('\n')[0]);

        // El archivo es Latin1, así que un carácter equivale a un byte.
        Assert.Equal("xref", text.Substring(xrefOffset, 4));

        var entries = text[(xrefOffset..)].Split('\n')
            .Where(l => l.EndsWith(" n ") || l.EndsWith(" f "))
            .ToList();

        // Primera entrada es la libre, luego uno por objeto.
        for (var i = 1; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i][..10]);
            var expected = $"{i} 0 obj";
            Assert.Equal(expected, text.Substring(offset, expected.Length));
        }
    }

    [Fact]
    public void Pdf_declares_the_trailer_size_matching_the_objects()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(10)));

        var objects = CountOccurrences(pdf, " 0 obj");
        Assert.Contains($"/Size {objects + 1}", pdf);
    }

    /// <summary>
    /// Los hijos del árbol de páginas deben ser referencias «N 0 R», no enteros sueltos: un
    /// lector real interpreta el entero como un objeto de tipo incorrecto y muestra el
    /// documento en blanco, aunque el resto del archivo esté bien.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(31)]   // dos páginas: [5 0 R 7 0 R]
    public void Pdf_page_tree_kids_are_object_references(int tickets)
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(tickets)));
        var pages = (int)Math.Ceiling(tickets / 30.0);

        var kids = string.Join(" ", Enumerable.Range(0, pages).Select(p => $"{5 + (p * 2)} 0 R"));
        Assert.Contains($"/Kids [{kids}]", pdf);
    }

    /// <summary>Los paréntesis cierran las cadenas literales de PDF y deben ir escapados.</summary>
    [Fact]
    public void Pdf_escapes_parentheses_and_backslashes_in_text()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now,
            "plan (promo) \\ especial",
            TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1)", "1)")]);

        var pdf = Text(TicketPdfWriter.WriteSheet(batch));

        Assert.Contains(@"plan \(promo\) \\ especial", pdf);
        Assert.Contains(@"(1\)) Tj", pdf);
    }

    /// <summary>
    /// Helvetica con codificación WinAnsi solo llega hasta U+00FF. Un carácter fuera de ese
    /// rango debe sustituirse en vez de emitirse, o el lector mostraría basura.
    /// </summary>
    [Fact]
    public void Pdf_replaces_characters_outside_the_encoding()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "plan 日本語", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("123456", "123456")]);

        var pdf = Text(TicketPdfWriter.WriteSheet(batch));

        Assert.DoesNotContain("日本", pdf);
        Assert.Contains("plan ??", pdf);
    }

    [Fact]
    public void Pdf_keeps_accented_characters_as_winansi_bytes()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "plan económico", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("123456", "123456")]);

        var pdf = TicketPdfWriter.WriteSheet(batch);

        // 0xF3 es 'ó' en WinAnsi.
        Assert.Contains((byte)0xF3, pdf);
    }

    /// <summary>La referencia de una tarjeta con texto largo debe encogerse hasta caber.</summary>
    [Fact]
    public void Pdf_shrinks_the_code_font_when_the_text_is_long()
    {
        var longCode = new TicketBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("12345678901234567890", "x")]);
        var shortCode = TicketBatchExportWith("123456");

        var longSize = FontSizeOf(TicketPdfWriter.WriteSheet(longCode), "12345678901234567890");
        var shortSize = FontSizeOf(TicketPdfWriter.WriteSheet(shortCode), "123456");

        Assert.True(longSize < shortSize, $"esperaba {longSize} < {shortSize}");
    }

    [Fact]
    public void Pdf_handles_a_single_ticket()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(1)));

        Assert.Contains("/Count 1", pdf);
        Assert.Contains("(100000) Tj", pdf);
    }

    /// <summary>
    /// La tarjeta reúne red, precio y duración junto al QR, que es lo que el cliente necesita
    /// para conectarse y lo que el vendedor necesita cobrar.
    /// </summary>
    [Fact]
    public void Pdf_card_shows_wifi_price_duration_and_qr_when_provided()
    {
        var batch = new TicketBatchExport(
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1249904", "1249904")],
            Brand: "NextGen", Ssid: "GB_nextGen_PA01", Price: 15m, StatusUrl: "gbnextgen.mx");

        var pdf = Text(TicketPdfWriter.WriteSheet(batch));

        Assert.Contains("Red: GB_nextGen_PA01", pdf);
        Assert.Contains("(1249904) Tj", pdf);
        Assert.Contains("$ 15.00 · Duración: 1 h", pdf);
        // El QR se pinta como rectángulos negros: debe haber muchos.
        Assert.True(CountOccurrences(pdf, " re f") > 20, "esperaba módulos del QR en el PDF");
    }

    [Fact]
    public void Pdf_card_shows_duration_even_without_optional_data()
    {
        var pdf = Text(TicketPdfWriter.WriteSheet(Batch(1)));

        Assert.DoesNotContain("Red:", pdf);
        Assert.DoesNotContain("Wi-Fi:", pdf);
        Assert.DoesNotContain("$ ", pdf);
        Assert.Contains("Duración: 2 h", pdf);
    }

    /// <summary>
    /// El ticket individual cabe en un rollo de 80 mm (226,8 pt) y ocupa una sola página. La
    /// altura de la página es exactamente la de la tarjeta: en un rollo, el sobrante se corta y
    /// se tira, así que cualquier margen vertical es papel desechado.
    /// </summary>
    [Fact]
    public void Pdf_thermal_fits_an_80mm_roll_with_a_single_page()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1249904", "1249904")], Brand: "NextGen");

        var pdf = Text(TicketPdfWriter.WriteThermal(batch, batch.Tickets[0]));

        // Marca: 10 + 11 (título) + 28 (código) + 144 (QR 140 + 4) + 16 (datos) + 10.
        Assert.Contains("/MediaBox [0 0 226.8 219]", pdf);
        Assert.Equal(1, CountOccurrences(pdf, "/Type /Page "));
        Assert.Contains("(1249904) Tj", pdf);
        Assert.Contains("Duración: 1 h", pdf);
    }

    /// <summary>
    /// Sin marca la tarjeta encoge a código, QR, precio/duración y nada más. La página térmica
    /// debe medir exactamente eso, ni un punto más.
    /// </summary>
    [Fact]
    public void Pdf_thermal_page_height_matches_the_content_without_optional_lines()
    {
        var batch = TicketBatchExportWith("1249904");

        var pdf = Text(TicketPdfWriter.WriteThermal(batch, batch.Tickets[0]));

        // 10 + 28 (código) + 144 (QR 140 + 4) + 16 (datos) + 10.
        Assert.Contains("/MediaBox [0 0 226.8 208]", pdf);
    }

    /// <summary>
    /// En térmica no se pinta el marco gris de la hoja: el borde del papel ya separa el ticket
    /// y el marco sería tinta gastada en un recibo desechable.
    /// </summary>
    [Fact]
    public void Pdf_thermal_draws_no_cutting_frame()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1249904", "1249904")], Brand: "NextGen");

        var pdf = Text(TicketPdfWriter.WriteThermal(batch, batch.Tickets[0]));

        Assert.DoesNotContain(" re S", pdf);
    }

    [Theory]
    [InlineData(null, "1249904", "1249904")]
    [InlineData("", "1249904", "1249904")]
    [InlineData("https://gbnextgen.mx/estado/{codigo}", "1249904", "https://gbnextgen.mx/estado/1249904")]
    [InlineData("https://gbnextgen.mx/estado", "1249904", "https://gbnextgen.mx/estado/1249904")]
    [InlineData("https://gbnextgen.mx/estado/", "1249904", "https://gbnextgen.mx/estado/1249904")]
    public void Qr_payload_uses_the_code_or_embeds_it_in_the_status_url(string? url, string code, string expected)
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser(code, code)], StatusUrl: url);

        Assert.Equal(expected, batch.QrPayloadFor(new CreatedHotspotUser(code, code)));
    }

    private static TicketBatchExport TicketBatchExportWith(string code) => new(
        DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
        [new CreatedHotspotUser(code, code)]);

    private static double FontSizeOf(byte[] pdf, string code)
    {
        var text = Text(pdf);
        var marker = $"({code}) Tj";
        var end = text.IndexOf(marker, StringComparison.Ordinal);
        var lineStart = text.LastIndexOf("BT ", end, StringComparison.Ordinal);
        var line = text[lineStart..end];

        var tokens = line.Split(' ');
        var fontIndex = Array.FindIndex(tokens, token => token is "/F1" or "/F2");
        return double.Parse(tokens[fontIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}

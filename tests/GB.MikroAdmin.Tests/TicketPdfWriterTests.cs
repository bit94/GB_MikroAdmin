using System.Text;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

/// <summary>
/// Comprueba la estructura del PDF sin depender de una librería lectora: se verifican las
/// marcas que todo lector exige y los desplazamientos de la tabla de referencias cruzadas, que
/// es la parte que más fácilmente se rompe al escribir el archivo a mano.
/// </summary>
public sealed class VoucherPdfWriterTests
{
    private static VoucherBatchExport Batch(int count, string profile = "limitado", int hours = 2) => new(
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
        var pdf = Text(VoucherPdfWriter.Write(Batch(5)));

        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.EndsWith("%%EOF\n", pdf);
    }

    [Fact]
    public void Pdf_declares_a_catalog_and_a_page_tree()
    {
        var pdf = Text(VoucherPdfWriter.Write(Batch(5)));

        Assert.Contains("/Type /Catalog", pdf);
        Assert.Contains("/Type /Pages", pdf);
        Assert.Contains("/Root 1 0 R", pdf);
    }

    [Fact]
    public void Pdf_embeds_no_font_file_and_uses_a_standard_one()
    {
        var pdf = Text(VoucherPdfWriter.Write(Batch(5)));

        Assert.Contains("/BaseFont /Helvetica", pdf);
        // Helvetica es estándar: incrustarla engordaría el archivo sin necesidad.
        Assert.DoesNotContain("/FontFile", pdf);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(27, 1)]   // justo la capacidad de una hoja
    [InlineData(28, 2)]   // un voucher más y salta de página
    [InlineData(54, 2)]
    [InlineData(55, 3)]
    public void Pdf_paginates_at_the_cards_per_page(int vouchers, int expectedPages)
    {
        var pdf = Text(VoucherPdfWriter.Write(Batch(vouchers)));

        Assert.Contains($"/Count {expectedPages}", pdf);
        Assert.Equal(expectedPages, CountOccurrences(pdf, "/Type /Page "));
    }

    [Fact]
    public void Pdf_includes_every_code()
    {
        var pdf = Text(VoucherPdfWriter.Write(Batch(20)));

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
        var pdf = VoucherPdfWriter.Write(Batch(30))
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
        var pdf = Text(VoucherPdfWriter.Write(Batch(10)));

        var objects = CountOccurrences(pdf, " 0 obj");
        Assert.Contains($"/Size {objects + 1}", pdf);
    }

    /// <summary>Los paréntesis cierran las cadenas literales de PDF y deben ir escapados.</summary>
    [Fact]
    public void Pdf_escapes_parentheses_and_backslashes_in_text()
    {
        var batch = new VoucherBatchExport(
            DateTimeOffset.Now,
            "plan (promo) \\ especial",
            TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1)", "1)")]);

        var pdf = Text(VoucherPdfWriter.Write(batch));

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
        var batch = new VoucherBatchExport(
            DateTimeOffset.Now, "plan 日本語", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("123456", "123456")]);

        var pdf = Text(VoucherPdfWriter.Write(batch));

        Assert.DoesNotContain("日本", pdf);
        Assert.Contains("plan ??", pdf);
    }

    [Fact]
    public void Pdf_keeps_accented_characters_as_winansi_bytes()
    {
        var batch = new VoucherBatchExport(
            DateTimeOffset.Now, "plan económico", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("123456", "123456")]);

        var pdf = VoucherPdfWriter.Write(batch);

        // 0xF3 es 'ó' en WinAnsi.
        Assert.Contains((byte)0xF3, pdf);
    }

    /// <summary>La referencia de una tarjeta con texto largo debe encogerse hasta caber.</summary>
    [Fact]
    public void Pdf_shrinks_the_code_font_when_the_text_is_long()
    {
        var longCode = new VoucherBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("12345678901234567890", "x")]);
        var shortCode = VoucherBatchExportWith("123456");

        var longSize = FontSizeOf(VoucherPdfWriter.Write(longCode), "12345678901234567890");
        var shortSize = FontSizeOf(VoucherPdfWriter.Write(shortCode), "123456");

        Assert.True(longSize < shortSize, $"esperaba {longSize} < {shortSize}");
    }

    [Fact]
    public void Pdf_handles_a_single_voucher()
    {
        var pdf = Text(VoucherPdfWriter.Write(Batch(1)));

        Assert.Contains("/Count 1", pdf);
        Assert.Contains("(100000) Tj", pdf);
    }

    private static VoucherBatchExport VoucherBatchExportWith(string code) => new(
        DateTimeOffset.Now, "limitado", TimeSpan.FromHours(1),
        [new CreatedHotspotUser(code, code)]);

    private static double FontSizeOf(byte[] pdf, string code)
    {
        var text = Text(pdf);
        var marker = $"({code}) Tj";
        var end = text.IndexOf(marker, StringComparison.Ordinal);
        var lineStart = text.LastIndexOf("BT ", end, StringComparison.Ordinal);
        var line = text[lineStart..end];

        var sizeIndex = line.IndexOf(" /F1 ", StringComparison.Ordinal) + 5;
        var sizeEnd = line.IndexOf(" Tf", sizeIndex, StringComparison.Ordinal);

        return double.Parse(line[sizeIndex..sizeEnd], System.Globalization.CultureInfo.InvariantCulture);
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

using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TicketExporterTests
{
    private static TicketBatchExport Batch(params string[] codes) => new(
        new DateTimeOffset(2026, 9, 26, 14, 30, 0, TimeSpan.Zero),
        "limitado",
        TimeSpan.FromHours(2),
        codes.Select(c => new CreatedHotspotUser(c, c)).ToList());

    [Fact]
    public void Csv_starts_with_a_header()
    {
        var csv = TicketExporter.ToCsv(Batch("123456"));

        Assert.StartsWith("Codigo;Usuario;Contrasena;Perfil;Duracion;DuracionTexto;Creado", csv);
    }

    [Fact]
    public void Csv_writes_one_line_per_ticket()
    {
        var lines = TicketExporter.ToCsv(Batch("111111", "222222", "333333"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Encabezado más una línea por ticket.
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void Csv_repeats_the_code_as_user_and_password()
    {
        var line = TicketExporter.ToCsv(Batch("445566")).Split('\n')[1].Trim().Split(';');

        Assert.Equal("445566", line[0]);
        Assert.Equal("445566", line[1]);
        Assert.Equal("445566", line[2]);
    }

    [Fact]
    public void Csv_includes_profile_duration_and_creation_date()
    {
        var line = TicketExporter.ToCsv(Batch("100000")).Split('\n')[1];

        Assert.Contains("limitado", line);
        Assert.Contains("2 h", line);
        Assert.Contains("2026-09-26 14:30", line);
    }

    /// <summary>
    /// El destino habitual es Excel en configuración regional española, donde la coma es el
    /// separador decimal: por eso el CSV se separa con punto y coma.
    /// </summary>
    [Fact]
    public void Csv_uses_semicolon_as_the_separator()
    {
        var line = TicketExporter.ToCsv(Batch("100000")).Split('\n')[1];

        Assert.Contains(';', line);
        Assert.DoesNotContain(',', line);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("con;punto", "\"con;punto\"")]
    [InlineData("con\"comilla", "\"con\"\"comilla\"")]
    [InlineData("con\nsalto", "\"con\nsalto\"")]
    public void Escape_quotes_only_when_the_field_needs_it(string value, string expected)
        => Assert.Equal(expected, TicketExporter.Escape(value));

    [Fact]
    public void Escape_returns_empty_for_null()
        => Assert.Equal(string.Empty, TicketExporter.Escape(null));

    [Fact]
    public void PlainList_has_a_header_and_one_code_per_line()
    {
        var lines = TicketExporter.ToPlainList(Batch("111111", "222222"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();

        Assert.Contains("2", lines[0]);
        Assert.Contains("limitado", lines[0]);
        Assert.Equal("111111", lines[1]);
        Assert.Equal("222222", lines[2]);
    }

    [Theory]
    [InlineData(0, 1, 0, "1 h")]
    [InlineData(0, 0, 30, "30 min")]
    [InlineData(2, 0, 0, "2 d")]
    public void Humanize_uses_plain_language(int days, int hours, int minutes, string expected)
        => Assert.Equal(expected, TicketBatchExport.Humanize(new TimeSpan(days, hours, minutes, 0)));

    [Fact]
    public void Humanize_falls_back_to_clock_format_for_odd_durations()
        => Assert.Equal("01:02:03", TicketBatchExport.Humanize(new TimeSpan(1, 2, 3)));
}

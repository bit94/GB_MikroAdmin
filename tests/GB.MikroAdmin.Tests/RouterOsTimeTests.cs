using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class RouterOsTimeTests
{
    [Theory]
    [InlineData(0, 1, 1, 0, "1h1m")]
    [InlineData(0, 0, 30, 0, "30m")]
    [InlineData(0, 0, 0, 45, "45s")]
    [InlineData(1, 0, 0, 0, "1d")]
    public void ToRouterOs_writes_compact_notation(int days, int hours, int minutes, int seconds, string expected)
        => Assert.Equal(expected, RouterOsTime.ToRouterOs(new TimeSpan(days, hours, minutes, seconds)));

    /// <summary>
    /// El caso que estaba roto: 7 días se emitía como "168h0m", y RouterOS no acepta que una
    /// sola unidad desborde su ámbito, así que el límite quedaba mal configurado.
    /// </summary>
    [Fact]
    public void ToRouterOs_splits_seven_days_into_weeks()
        => Assert.Equal("1w", RouterOsTime.ToRouterOs(TimeSpan.FromDays(7)));

    [Fact]
    public void ToRouterOs_splits_durations_over_a_day_into_days_and_hours()
        => Assert.Equal("2d3h", RouterOsTime.ToRouterOs(TimeSpan.FromDays(2) + TimeSpan.FromHours(3)));

    [Fact]
    public void ToRouterOs_rejects_non_positive_durations()
        => Assert.Throws<ArgumentOutOfRangeException>(() => RouterOsTime.ToRouterOs(TimeSpan.Zero));

    [Theory]
    [InlineData("1h30m", 1, 30, 0)]
    [InlineData("1w2d", 216, 0, 0)]
    [InlineData("45s", 0, 0, 45)]
    [InlineData("2d", 48, 0, 0)]
    public void Parse_reads_multi_component_durations(string text, int hours, int minutes, int seconds)
        => Assert.Equal(TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds),
            RouterOsTime.Parse(text));

    [Fact]
    public void Parse_returns_null_for_empty_input()
        => Assert.Null(RouterOsTime.Parse("  "));

    /// <summary>
    /// Antes un carácter desconocido devolvía cero en silencio, con lo que un límite ilegible
    /// hacía que el voucher pareciera ilimitado. Ahora falla de forma explícita.
    /// </summary>
    [Theory]
    [InlineData("1h30x")]
    [InlineData("h30")]
    [InlineData("1h30")]
    public void Parse_throws_on_malformed_input(string text)
        => Assert.Throws<FormatException>(() => RouterOsTime.Parse(text));

    [Fact]
    public void ToRouterOs_and_Parse_are_inverse_for_typical_plans()
    {
        foreach (var duration in new[]
                 {
                     TimeSpan.FromMinutes(30),
                     TimeSpan.FromHours(1),
                     TimeSpan.FromHours(12),
                     TimeSpan.FromDays(1),
                     TimeSpan.FromDays(7),
                     TimeSpan.FromDays(30)
                 })
        {
            Assert.Equal(duration, RouterOsTime.Parse(RouterOsTime.ToRouterOs(duration)));
        }
    }
}

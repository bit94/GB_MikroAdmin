using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class ProfileMarkerTests
{
    [Fact]
    public void Build_writes_the_documented_prefix()
    {
        var comment = ProfileMarker.Build(TimeSpan.FromHours(2));

        Assert.Equal("GBM:voucher-duration=2h", comment);
    }

    [Fact]
    public void Read_returns_the_declared_duration()
    {
        Assert.Equal(TimeSpan.FromHours(2), ProfileMarker.ReadTicketDuration("GBM:voucher-duration=2h"));
    }

    [Fact]
    public void Read_returns_null_when_the_marker_is_absent()
    {
        Assert.Null(ProfileMarker.ReadTicketDuration("plan de mostrador"));
        Assert.Null(ProfileMarker.ReadTicketDuration(null));
        Assert.Null(ProfileMarker.ReadTicketDuration("   "));
    }

    [Fact]
    public void Read_returns_null_when_the_marker_has_an_empty_value()
        => Assert.Null(ProfileMarker.ReadTicketDuration("GBM:voucher-duration="));

    /// <summary>Un comentario puede llevar notas añadidas después del marcador.</summary>
    [Fact]
    public void Read_ignores_trailing_notes()
        => Assert.Equal(TimeSpan.FromHours(1), ProfileMarker.ReadTicketDuration("GBM:voucher-duration=1h promo mayo"));

    /// <summary>Un comentario puede llevar notas escritas antes del marcador.</summary>
    [Fact]
    public void Read_ignores_leading_notes()
        => Assert.Equal(TimeSpan.FromHours(1), ProfileMarker.ReadTicketDuration("mostrador GBM:voucher-duration=1h"));

    [Fact]
    public void Build_and_Read_are_inverse()
    {
        var duration = TimeSpan.FromDays(30);

        Assert.Equal(duration, ProfileMarker.ReadTicketDuration(ProfileMarker.Build(duration)));
    }

    [Fact]
    public void Read_throws_on_a_corrupt_value()
        => Assert.Throws<FormatException>(() => ProfileMarker.ReadTicketDuration("GBM:voucher-duration=abc"));
}

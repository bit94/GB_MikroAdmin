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

    [Fact]
    public void WithDuration_replaces_the_value_and_keeps_surrounding_notes()
        => Assert.Equal(
            "mostrador GBM:voucher-duration=2h promo mayo",
            ProfileMarker.WithDuration("mostrador GBM:voucher-duration=1h promo mayo", TimeSpan.FromHours(2)));

    [Fact]
    public void WithDuration_adds_the_marker_to_a_comment_without_one()
        => Assert.Equal(
            "plan de mostrador GBM:voucher-duration=2h",
            ProfileMarker.WithDuration("plan de mostrador", TimeSpan.FromHours(2)));

    [Fact]
    public void WithDuration_writes_only_the_marker_when_the_comment_is_empty()
    {
        Assert.Equal("GBM:voucher-duration=2h", ProfileMarker.WithDuration(null, TimeSpan.FromHours(2)));
        Assert.Equal("GBM:voucher-duration=2h", ProfileMarker.WithDuration("   ", TimeSpan.FromHours(2)));
    }

    [Fact]
    public void WithDuration_produces_a_comment_readable_by_Read()
    {
        var comment = ProfileMarker.WithDuration("nota GBM:voucher-duration=30m otra nota", TimeSpan.FromDays(30));

        Assert.Equal(TimeSpan.FromDays(30), ProfileMarker.ReadTicketDuration(comment));
        Assert.Equal("nota GBM:voucher-duration=4w2d otra nota", comment);
    }
}

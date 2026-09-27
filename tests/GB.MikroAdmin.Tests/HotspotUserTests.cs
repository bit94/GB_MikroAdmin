using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class HotspotUserTests
{
    [Fact]
    public void Remaining_is_limit_minus_uptime()
    {
        var user = new HotspotUser("a", "p", TimeSpan.FromHours(2), TimeSpan.FromMinutes(30), true);

        Assert.Equal(TimeSpan.FromMinutes(90), user.Remaining);
    }

    [Fact]
    public void Remaining_never_goes_negative_once_expired()
    {
        var user = new HotspotUser("a", "p", TimeSpan.FromHours(1), TimeSpan.FromHours(3), false);

        Assert.Equal(TimeSpan.Zero, user.Remaining);
        Assert.True(user.IsExpired);
    }

    [Fact]
    public void Remaining_is_null_without_a_limit()
    {
        var user = new HotspotUser("a", "p", null, TimeSpan.FromHours(5), true);

        Assert.Null(user.Remaining);
        Assert.False(user.IsExpired);
    }

    [Theory]
    [InlineData(null, 0, TicketState.Unlimited)]
    [InlineData(60, 0, TicketState.Available)]
    [InlineData(60, 30, TicketState.InUse)]
    [InlineData(60, 60, TicketState.Expired)]
    [InlineData(60, 90, TicketState.Expired)]
    public void State_reflects_consumption(int? limitMinutes, int uptimeMinutes, TicketState expected)
    {
        TimeSpan? limit = limitMinutes is null ? null : TimeSpan.FromMinutes(limitMinutes.Value);
        var user = new HotspotUser("a", "p", limit, TimeSpan.FromMinutes(uptimeMinutes), true);

        Assert.Equal(expected, user.State);
    }

    [Fact]
    public void ConsumedPercent_spans_zero_to_one_hundred()
    {
        var user = new HotspotUser("a", "p", TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), true);

        Assert.Equal(25, user.ConsumedPercent);
    }

    [Fact]
    public void ConsumedPercent_is_capped_at_one_hundred()
    {
        var user = new HotspotUser("a", "p", TimeSpan.FromHours(1), TimeSpan.FromHours(5), true);

        Assert.Equal(100, user.ConsumedPercent);
    }

    [Fact]
    public void ConsumedPercent_is_zero_for_unlimited_or_invalid_limits()
    {
        Assert.Equal(0, new HotspotUser("a", "p", null, TimeSpan.FromHours(5), true).ConsumedPercent);
        Assert.Equal(0, new HotspotUser("a", "p", TimeSpan.Zero, TimeSpan.FromHours(5), true).ConsumedPercent);
    }
}

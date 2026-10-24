using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TicketExpiryTrackerTests
{
    [Fact]
    public void Existing_expired_tickets_notify_on_the_first_observation()
    {
        var tracker = new TicketExpiryTracker();

        var expired = tracker.Observe([Expired("123456"), Online("654321")]);

        Assert.Equal("123456", Assert.Single(expired).Name);
    }

    [Fact]
    public void The_same_ticket_is_not_notified_twice()
    {
        var tracker = new TicketExpiryTracker();
        tracker.Observe([Expired("123456")]);

        var expired = tracker.Observe([Expired("123456")]);

        Assert.Empty(expired);
    }

    [Fact]
    public void A_ticket_that_still_has_time_is_ignored()
    {
        var tracker = new TicketExpiryTracker();

        Assert.Empty(tracker.Observe([Online("654321")]));
    }

    [Fact]
    public void A_removed_ticket_can_notify_again_if_its_code_is_reused()
    {
        var tracker = new TicketExpiryTracker();
        tracker.Observe([Expired("123456")]);
        tracker.Observe([]);

        var expired = tracker.Observe([Expired("123456")]);

        Assert.Single(expired);
    }

    [Fact]
    public void Notifications_do_not_repeat_because_of_letter_case()
    {
        var tracker = new TicketExpiryTracker();
        tracker.Observe([Expired("ABC123")]);

        Assert.Empty(tracker.Observe([Expired("abc123")]));
    }

    private static HotspotUser Expired(string name) =>
        new(name, "limitado", TimeSpan.FromHours(1), TimeSpan.FromHours(1), false);

    private static HotspotUser Online(string name) =>
        new(name, "limitado", TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), true);
}

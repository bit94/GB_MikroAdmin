using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TicketExpiryMonitorTests
{
    [Fact]
    public async Task Poll_notifies_once_when_a_ticket_expires()
    {
        var source = new FakeSource { Users = [Expired("123456")] };
        var notifier = new FakeNotifier();
        var monitor = Create(source, notifier);

        await monitor.PollOnceAsync();
        await monitor.PollOnceAsync();

        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal("123456", Assert.Single(notification).Name);
        Assert.Equal(0, notifier.Clears);
    }

    [Fact]
    public async Task Poll_keeps_the_notification_while_expired_tickets_are_still_there()
    {
        var source = new FakeSource { Users = [Expired("123456")] };
        var notifier = new FakeNotifier();
        var monitor = Create(source, notifier);

        await monitor.PollOnceAsync();
        await monitor.PollOnceAsync();
        await monitor.PollOnceAsync();

        Assert.Single(notifier.Notifications);
        Assert.Equal(0, notifier.Clears);
    }

    [Fact]
    public async Task Poll_does_nothing_without_a_connection()
    {
        var source = new FakeSource { IsConnected = false, Users = [Expired("123456")] };
        var notifier = new FakeNotifier();
        var monitor = Create(source, notifier);

        await monitor.PollOnceAsync();

        Assert.Empty(notifier.Notifications);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Poll_clears_the_previous_notification_when_expired_tickets_are_gone()
    {
        var source = new FakeSource { Users = [Expired("123456")] };
        var notifier = new FakeNotifier();
        var monitor = Create(source, notifier);

        await monitor.PollOnceAsync();
        source.Users = [Online("654321")];
        await monitor.PollOnceAsync();

        Assert.Equal(1, notifier.Clears);
    }

    private static TicketExpiryMonitor Create(FakeSource source, FakeNotifier notifier) =>
        new(source, notifier, new TicketExpiryTracker(), TimeSpan.FromMinutes(1));

    private static HotspotUser Expired(string name) =>
        new(name, "limitado", TimeSpan.FromHours(1), TimeSpan.FromHours(1), false);

    private static HotspotUser Online(string name) =>
        new(name, "limitado", TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), true);

    private sealed class FakeSource : ITicketSource
    {
        public bool IsConnected { get; set; } = true;
        public IReadOnlyList<HotspotUser> Users { get; set; } = [];
        public int Calls { get; private set; }

        public Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Users);
        }
    }

    private sealed class FakeNotifier : ITicketNotifier
    {
        public List<IReadOnlyList<HotspotUser>> Notifications { get; } = [];
        public int Clears { get; private set; }

        public Task EnsurePermissionAsync() => Task.CompletedTask;

        public Task NotifyExpiredAsync(IReadOnlyList<HotspotUser> tickets)
        {
            Notifications.Add(tickets);
            return Task.CompletedTask;
        }

        public Task ClearExpiredAsync()
        {
            Clears++;
            return Task.CompletedTask;
        }
    }
}

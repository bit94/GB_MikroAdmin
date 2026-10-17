using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TickFakeConnectionTests
{
    [Fact]
    public async Task AddUser_appears_with_the_requested_limit()
    {
        await using var router = new TickFakeConnection();
        await router.ConnectAsync(new("router", 8729, "admin", "secret", true, true), CancellationToken.None);
        await router.AddUserAsync("cliente-1", "no-se-expone", "limitado", TimeSpan.FromHours(1), CancellationToken.None);

        var user = Assert.Single(await router.UsersAsync(CancellationToken.None), x => x.Name == "cliente-1");
        Assert.Equal(TimeSpan.FromHours(1), user.LimitUptime);
    }

    [Fact]
    public async Task RemoveUsers_removes_the_selected_account()
    {
        await using var router = new TickFakeConnection();
        await router.AddUserAsync("cliente-2", "secret", "limitado", TimeSpan.FromHours(1), CancellationToken.None);

        var removed = await router.RemoveUsersAsync(["cliente-2"], CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.DoesNotContain(await router.UsersAsync(CancellationToken.None), x => x.Name == "cliente-2");
    }

    [Fact]
    public async Task RemoveUsers_is_idempotent_on_the_simulator()
    {
        await using var router = new TickFakeConnection();

        var removed = await router.RemoveUsersAsync(["no-existe"], CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.Equal(0, router.UserCount);
    }

    [Fact]
    public async Task AddUser_rejects_duplicates()
    {
        await using var router = new TickFakeConnection();
        await router.AddUserAsync("dup", "1", "limitado", TimeSpan.FromHours(1), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<RouterWriteException>(
            () => router.AddUserAsync("DUP", "1", "limitado", TimeSpan.FromHours(1), CancellationToken.None));
        Assert.Equal(RouterWriteFailureKind.Duplicate, exception.Kind);
    }

    [Fact]
    public async Task AddProfile_rejects_duplicates()
    {
        await using var router = new TickFakeConnection();
        await router.AddProfileAsync("plan", "2M/2M", TimeSpan.FromHours(1), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.AddProfileAsync("plan", "4M/4M", TimeSpan.FromHours(2), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProfile_changes_rate_and_duration_keeping_the_name()
    {
        await using var router = new TickFakeConnection();
        await router.AddProfileAsync("plan", "2M/2M", TimeSpan.FromHours(1), CancellationToken.None);

        await router.UpdateProfileAsync("plan", "4M/4M", TimeSpan.FromHours(3), CancellationToken.None);

        var profile = Assert.Single(await router.ProfilesAsync(CancellationToken.None), x => x.Name == "plan");
        Assert.Equal("4M/4M", profile.RateLimit);
        Assert.Equal(TimeSpan.FromHours(3), profile.TicketDuration);
    }

    [Fact]
    public async Task UpdateProfile_fails_when_the_profile_was_removed()
    {
        await using var router = new TickFakeConnection();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.UpdateProfileAsync("plan", "4M/4M", TimeSpan.FromHours(3), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveProfile_deletes_it_from_the_simulator()
    {
        await using var router = new TickFakeConnection();
        await router.AddProfileAsync("plan", "2M/2M", TimeSpan.FromHours(1), CancellationToken.None);

        var removed = await router.RemoveProfileAsync("plan", CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.DoesNotContain(await router.ProfilesAsync(CancellationToken.None), x => x.Name == "plan");
    }

    [Fact]
    public async Task RemoveProfile_is_idempotent_on_the_simulator()
    {
        await using var router = new TickFakeConnection();

        var removed = await router.RemoveProfileAsync("no-existe", CancellationToken.None);

        Assert.Equal(0, removed);
    }
}

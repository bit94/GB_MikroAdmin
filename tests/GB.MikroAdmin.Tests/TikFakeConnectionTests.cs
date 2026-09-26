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
    public async Task RemoveUser_removes_the_selected_account()
    {
        await using var router = new TickFakeConnection();
        await router.AddUserAsync("cliente-2", "secret", "limitado", TimeSpan.FromHours(1), CancellationToken.None);
        await router.RemoveUserAsync("cliente-2", CancellationToken.None);

        Assert.DoesNotContain(await router.UsersAsync(CancellationToken.None), x => x.Name == "cliente-2");
    }

    [Fact]
    public async Task RemoveUser_is_idempotent_on_the_simulator()
    {
        await using var router = new TickFakeConnection();
        await router.RemoveUserAsync("no-existe", CancellationToken.None);

        Assert.Equal(0, router.UserCount);
    }

    [Fact]
    public async Task AddUser_rejects_duplicates()
    {
        await using var router = new TickFakeConnection();
        await router.AddUserAsync("dup", "1", "limitado", TimeSpan.FromHours(1), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.AddUserAsync("DUP", "1", "limitado", TimeSpan.FromHours(1), CancellationToken.None));
    }

    [Fact]
    public async Task AddProfile_rejects_duplicates()
    {
        await using var router = new TickFakeConnection();
        await router.AddProfileAsync("plan", "2M/2M", TimeSpan.FromHours(1), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.AddProfileAsync("plan", "4M/4M", TimeSpan.FromHours(2), CancellationToken.None));
    }
}

using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class Tik4NetRouterTransportTests
{
    private static RouterSettings RealSettings => new("192.168.88.1", 8729, "admin", "secret", UseTls: true, Simulated: false);

    [Fact]
    public async Task RemoveUsers_resolves_ids_and_sends_a_single_batched_remove()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/print"] =
                [
                    Row((".id", "*1A"), ("name", "111111")),
                    Row((".id", "*1B"), ("name", "222222"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        var removed = await transport.RemoveUsersAsync(["111111", "222222"], CancellationToken.None);

        Assert.Equal(2, removed);
        var call = Assert.Single(session.Executions);
        Assert.Equal("/ip/hotspot/user/remove", call.Command);
        // Pasar nombres en "numbers" fallaba contra un equipo real: RouterOS espera índice o rango.
        Assert.Equal("*1A,*1B", call.Parameters["numbers"]);
    }

    [Fact]
    public async Task RemoveUsers_with_a_single_name_removes_just_that_user()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/print"] =
                [
                    Row((".id", "*1A"), ("name", "111111")),
                    Row((".id", "*1B"), ("name", "222222"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        var removed = await transport.RemoveUsersAsync(["222222"], CancellationToken.None);

        Assert.Equal(1, removed);
        var call = Assert.Single(session.Executions);
        Assert.Equal("*1B", call.Parameters["numbers"]);
    }

    [Fact]
    public async Task RemoveUsers_ignores_names_that_no_longer_exist()
    {
        var session = new FakeSession
        {
            Rows = { ["/ip/hotspot/user/print"] = [Row((".id", "*1A"), ("name", "111111"))] }
        };

        await using var transport = await ConnectAsync(session);
        var removed = await transport.RemoveUsersAsync(["999999"], CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.Empty(session.Executions);
    }

    [Fact]
    public async Task AddUser_sends_the_limit_in_routeros_notation()
    {
        var session = new FakeSession();
        await using var transport = await ConnectAsync(session);

        await transport.AddUserAsync("123456", "123456", "limitado", TimeSpan.FromDays(7), CancellationToken.None);

        var call = Assert.Single(session.Executions);
        Assert.Equal("/ip/hotspot/user/add", call.Command);
        Assert.Equal("1w", call.Parameters["limit-uptime"]);
    }

    [Fact]
    public async Task AddProfile_writes_the_marker_into_the_comment()
    {
        var session = new FakeSession();
        await using var transport = await ConnectAsync(session);

        await transport.AddProfileAsync("plan", "2M/2M", TimeSpan.FromHours(3), CancellationToken.None);

        var call = Assert.Single(session.Executions);
        Assert.Equal("GBM:voucher-duration=3h", call.Parameters["comment"]);
    }

    [Fact]
    public async Task UpdateProfile_resolves_the_id_and_preserves_the_comment_notes()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/profile/print"] =
                [
                    Row((".id", "*5"), ("name", "plan"), ("comment", "mostrador GBM:voucher-duration=1h promo"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        await transport.UpdateProfileAsync("plan", "4M/4M", TimeSpan.FromHours(2), CancellationToken.None);

        var call = Assert.Single(session.Executions);
        Assert.Equal("/ip/hotspot/user/profile/set", call.Command);
        Assert.Equal("*5", call.Parameters["numbers"]);
        Assert.Equal("4M/4M", call.Parameters["rate-limit"]);
        Assert.Equal("mostrador GBM:voucher-duration=2h promo", call.Parameters["comment"]);
    }

    [Fact]
    public async Task UpdateProfile_fails_when_the_profile_no_longer_exists()
    {
        var session = new FakeSession();
        await using var transport = await ConnectAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.UpdateProfileAsync("plan", "4M/4M", TimeSpan.FromHours(2), CancellationToken.None));
        Assert.Empty(session.Executions);
    }

    [Fact]
    public async Task RemoveProfile_resolves_the_id_and_sends_a_remove()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/profile/print"] =
                [
                    Row((".id", "*5"), ("name", "plan")),
                    Row((".id", "*6"), ("name", "otro"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        var removed = await transport.RemoveProfileAsync("plan", CancellationToken.None);

        Assert.Equal(1, removed);
        var call = Assert.Single(session.Executions);
        Assert.Equal("/ip/hotspot/user/profile/remove", call.Command);
        Assert.Equal("*5", call.Parameters["numbers"]);
    }

    [Fact]
    public async Task RemoveProfile_ignores_a_profile_that_no_longer_exists()
    {
        var session = new FakeSession();
        await using var transport = await ConnectAsync(session);

        var removed = await transport.RemoveProfileAsync("plan", CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.Empty(session.Executions);
    }

    [Fact]
    public async Task Users_reads_limit_and_uptime_from_routeros_notation()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/print"] =
                [
                    Row(("name", "123456"), ("profile", "limitado"), ("limit-uptime", "1h"), ("uptime", "30m"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        var user = Assert.Single(await transport.UsersAsync(CancellationToken.None));

        Assert.Equal(TimeSpan.FromHours(1), user.LimitUptime);
        Assert.Equal(TimeSpan.FromMinutes(30), user.Uptime);
        Assert.Equal(TicketState.InUse, user.State);
    }

    [Fact]
    public async Task Profiles_reads_the_ticket_duration_from_the_comment()
    {
        var session = new FakeSession
        {
            Rows =
            {
                ["/ip/hotspot/user/profile/print"] =
                [
                    Row(("name", "limitado"), ("rate-limit", "2M/2M"), ("shared-users", "1"),
                        ("comment", "GBM:voucher-duration=2h30m"))
                ]
            }
        };

        await using var transport = await ConnectAsync(session);
        var profile = Assert.Single(await transport.ProfilesAsync(CancellationToken.None));

        Assert.Equal(TimeSpan.FromMinutes(150), profile.TicketDuration);
        Assert.Equal(1, profile.SharedUsers);
    }

    [Fact]
    public async Task Connect_rejects_a_non_tls_endpoint()
    {
        await using var transport = new Tik4NetRouterTransport(new FakeSessionFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.ConnectAsync(RealSettings with { UseTls = false }, CancellationToken.None));
    }

    [Fact]
    public async Task Connect_opens_the_session_with_the_requested_settings()
    {
        var factory = new FakeSessionFactory();
        await using var transport = new Tik4NetRouterTransport(factory);

        await transport.ConnectAsync(RealSettings, CancellationToken.None);

        Assert.True(factory.Opened);
        Assert.Equal(RealSettings, factory.Settings);
    }

    [Fact]
    public async Task Operations_require_an_active_connection()
    {
        await using var transport = new Tik4NetRouterTransport(new FakeSessionFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.UsersAsync(CancellationToken.None));
    }

    private static async Task<IRouterTransport> ConnectAsync(ITikSession session)
    {
        var transport = new Tik4NetRouterTransport(new FakeSessionFactory(session));
        await transport.ConnectAsync(RealSettings, CancellationToken.None);
        return transport;
    }

    private static ITikRow Row(params (string Field, string Value)[] fields) => new FakeRow(fields);

    private sealed class FakeRow(params (string Field, string Value)[] fields) : ITikRow
    {
        private readonly Dictionary<string, string> _fields =
            fields.ToDictionary(f => f.Field, f => f.Value, StringComparer.OrdinalIgnoreCase);

        public string GetString(string field) => GetStringOrNull(field) ?? string.Empty;

        public string? GetStringOrNull(string field) => _fields.TryGetValue(field, out var value) ? value : null;

        public bool GetBool(string field) => string.Equals(GetStringOrNull(field), "true", StringComparison.OrdinalIgnoreCase);

        public int GetInt(string field, int fallback) => int.TryParse(GetStringOrNull(field), out var v) ? v : fallback;
    }

    private sealed class FakeSession : ITikSession
    {
        public Dictionary<string, List<ITikRow>> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<(string Command, IReadOnlyDictionary<string, string> Parameters)> Executions { get; } = [];

        public Task<IReadOnlyList<ITikRow>> ListAsync(string command, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ITikRow>>(
                Rows.TryGetValue(command, out var rows) ? rows : []);

        public Task ExecuteAsync(string command, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            Executions.Add((command, parameters));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSessionFactory(ITikSession? session = null) : ITikSessionFactory
    {
        public bool Opened { get; private set; }

        public RouterSettings? Settings { get; private set; }

        public Task<ITikSession> OpenAsync(RouterSettings settings, CancellationToken cancellationToken)
        {
            Opened = true;
            Settings = settings;
            return Task.FromResult(session ?? new FakeSession());
        }
    }
}

using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class RouterServiceTests
{
    [Fact]
    public async Task Concurrent_reads_are_serialized_on_the_transport()
    {
        var transport = new ConcurrencyProbeTransport();
        var service = new RouterService(new StubSettingsStore(), new RouterTransportFactory(_ => transport));
        await service.ConnectAsync();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.GetUsersAsync()));

        Assert.Equal(1, transport.MaxConcurrency);
    }

    private sealed class StubSettingsStore : IRouterSettingsStore
    {
        public Task SaveAsync(RouterSettings settings) => Task.CompletedTask;

        public Task<RouterSettings?> GetAsync() =>
            Task.FromResult<RouterSettings?>(new RouterSettings("192.168.88.1", 8729, "admin", string.Empty, false, false));

        public void Clear()
        {
        }
    }

    private sealed class ConcurrencyProbeTransport : IRouterTransport
    {
        private int _active;

        public int MaxConcurrency { get; private set; }

        public Task ConnectAsync(RouterSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<RouterInterface>> InterfacesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RouterInterface>>([]);

        public async Task<IReadOnlyList<HotspotUser>> UsersAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            MaxConcurrency = Math.Max(MaxConcurrency, active);
            try
            {
                await Task.Delay(20, cancellationToken);
                return [];
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public Task<IReadOnlyList<HotspotProfile>> ProfilesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HotspotProfile>>([]);

        public Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateProfileAsync(string name, string rateLimit, TimeSpan ticketDuration, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<int> RemoveProfileAsync(string name, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> RemoveUsersAsync(IReadOnlyList<string> names, CancellationToken cancellationToken) => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

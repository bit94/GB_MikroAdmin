using GB.MikroAdmin.Models;
namespace GB.MikroAdmin.Services;

public interface IRouterService : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RouterInterface>> GetInterfacesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotspotUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotspotProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task AddUserAsync(string name, string password, string profile, TimeSpan limit, CancellationToken cancellationToken = default);
 Task AddProfileAsync(string name, string rateLimit, TimeSpan voucherDuration, CancellationToken cancellationToken = default);
    Task RemoveUserAsync(string name, CancellationToken cancellationToken = default);
}

using GB.MikroAdmin.Models;

namespace GB.MikroAdmin.Services;

public record CreatedHotspotUser(string Name, string Password);

public interface IHotspotProvisioningService
{
    Task<IReadOnlyList<CreatedHotspotUser>> CreateBatchAsync(
        int quantity,
        string profile,
        TimeSpan limit,
        CancellationToken cancellationToken = default);
}

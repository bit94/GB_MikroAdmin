using System.Security.Cryptography;

namespace GB.MikroAdmin.Services;

/// <summary>Coordina las altas de Hotspot sin exponer esos detalles a la interfaz.</summary>
public sealed class HotspotProvisioningService : IHotspotProvisioningService
{
    private readonly IRouterService _routerService;

    public HotspotProvisioningService(IRouterService routerService)
    {
        _routerService = routerService;
    }

    public async Task<IReadOnlyList<CreatedHotspotUser>> CreateBatchAsync(
        int quantity,
        string profile,
        TimeSpan limit,
        CancellationToken cancellationToken = default)
    {
        if (quantity is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe estar entre 1 y 100.");
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("Selecciona un perfil.", nameof(profile));

        var createdUsers = new List<CreatedHotspotUser>(quantity);
        for (var index = 1; index <= quantity; index++)
        {
            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            await _routerService.AddUserAsync(code, code, profile, limit, cancellationToken);
            createdUsers.Add(new CreatedHotspotUser(code, code));
        }

        return createdUsers;
    }
}

using GB.MikroAdmin.Models;

namespace GB.MikroAdmin.Services;

public interface IRouterSettingsStore
{
    Task SaveAsync(RouterSettings settings);
    Task<RouterSettings?> GetAsync();
    void Clear();
}

using GB.MikroAdmin.Models;
namespace GB.MikroAdmin.Services;

public sealed class RouterSettingsStore : IRouterSettingsStore
{
    const string Key = "router.settings.v1";
    public async Task SaveAsync(RouterSettings s) => await SecureStorage.Default.SetAsync(Key, System.Text.Json.JsonSerializer.Serialize(s));
    public async Task<RouterSettings?> GetAsync() { var json = await SecureStorage.Default.GetAsync(Key); return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<RouterSettings>(json); }
    public void Clear() => SecureStorage.Default.Remove(Key);
}

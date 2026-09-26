using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.Services;

/// <summary>
/// Almacena la configuración de conexión. En MAUI las credenciales van a SecureStorage, nunca
/// a preferencias planas ni a código. En pruebas se sustituye por <see cref="InMemoryRouterSettingsStore"/>.
/// </summary>
public sealed class RouterSettingsStore : IRouterSettingsStore
{
    private const string Key = "router.settings.v1";

    public Task SaveAsync(RouterSettings settings)
        => SecureStorage.Default.SetAsync(Key, System.Text.Json.JsonSerializer.Serialize(settings));

    public async Task<RouterSettings?> GetAsync()
    {
        var json = await SecureStorage.Default.GetAsync(Key);
        return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<RouterSettings>(json);
    }

    public void Clear() => SecureStorage.Default.Remove(Key);
}

/// <summary>Implementación sin persistencia, para pruebas automatizadas.</summary>
public sealed class InMemoryRouterSettingsStore : IRouterSettingsStore
{
    private RouterSettings? _settings;

    public Task SaveAsync(RouterSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<RouterSettings?> GetAsync() => Task.FromResult(_settings);

    public void Clear() => _settings = null;
}

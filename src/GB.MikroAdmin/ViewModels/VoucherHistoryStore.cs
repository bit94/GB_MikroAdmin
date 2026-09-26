using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

public sealed record VoucherBatch(
    DateTimeOffset CreatedAt,
    string Profile,
    TimeSpan Duration,
    IReadOnlyList<CreatedHotspotUser> Vouchers)
{
    public int Count => Vouchers.Count;

    public string DurationText => UsersViewModel.Humanize(Duration);

    public string Title => $"{CreatedAt:dd/MM/yyyy HH:mm} · {Count} vouchers · {Profile} · {DurationText}";
}

/// <summary>
/// Conserva los lotes generados. Sin esto, cerrar la aplicación perdía los códigos recién
/// creados aunque siguieran activos en el router, y no había forma de reimprimirlos.
/// </summary>
public interface IVoucherHistoryStore
{
    Task SaveAsync(VoucherBatch batch);
    Task<IReadOnlyList<VoucherBatch>> GetAllAsync();
}

/// <summary>Persistencia en el almacén local del dispositivo, en JSON.</summary>
public sealed class VoucherHistoryStore : IVoucherHistoryStore
{
    private const string Key = "voucher.history.v1";
    private const int MaxBatches = 100;

    public async Task SaveAsync(VoucherBatch batch)
    {
        var all = (await ReadAsync()).ToList();
        all.Insert(0, batch);
        if (all.Count > MaxBatches)
            all.RemoveRange(MaxBatches, all.Count - MaxBatches);

        await File.WriteAllTextAsync(PathFor(), System.Text.Json.JsonSerializer.Serialize(all));
    }

    public Task<IReadOnlyList<VoucherBatch>> GetAllAsync() => ReadAsync();

    private async Task<IReadOnlyList<VoucherBatch>> ReadAsync()
    {
        var path = PathFor();
        if (!File.Exists(path))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return System.Text.Json.JsonSerializer.Deserialize<List<VoucherBatch>>(json) ?? [];
        }
        catch (Exception)
        {
            // Un histórico corrupto no debe impedir usar la aplicación.
            return [];
        }
    }

    private static string PathFor()
        => Path.Combine(FileSystem.AppDataDirectory, "voucher-history.json");
}

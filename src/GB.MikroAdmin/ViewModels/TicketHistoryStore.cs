using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Conserva los lotes generados. Sin esto, cerrar la aplicación perdía los códigos recién
/// creados aunque siguieran activos en el router, y no había forma de reimprimirlos.
/// </summary>
public interface IVoucherHistoryStore
{
    Task SaveAsync(VoucherBatchExport batch);
    Task<IReadOnlyList<VoucherBatchExport>> GetAllAsync();
    Task RemoveAsync(VoucherBatchExport batch);
}

/// <summary>Persistencia en el almacén local del dispositivo, en JSON.</summary>
public sealed class VoucherHistoryStore : IVoucherHistoryStore
{
    private const string FileName = "voucher-history.json";
    private const int MaxBatches = 100;

    public async Task SaveAsync(VoucherBatchExport batch)
    {
        var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
        all.Insert(0, batch);
        if (all.Count > MaxBatches)
            all.RemoveRange(MaxBatches, all.Count - MaxBatches);

        await WriteAsync(all);
    }

    public Task<IReadOnlyList<VoucherBatchExport>> GetAllAsync() => ReadAsync();

    public async Task RemoveAsync(VoucherBatchExport batch)
    {
        // Se identifica por marca de tiempo: dos lotes no se crean en el mismo instante.
        var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
        await WriteAsync(all);
    }

    private async Task<IReadOnlyList<VoucherBatchExport>> ReadAsync()
    {
        var path = PathFor();
        if (!File.Exists(path))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return System.Text.Json.JsonSerializer.Deserialize<List<VoucherBatchExport>>(json) ?? [];
        }
        catch (Exception)
        {
            // Un histórico ilegible no debe impedir usar la aplicación.
            return [];
        }
    }

    private static async Task WriteAsync(IReadOnlyList<VoucherBatchExport> batches)
        => await File.WriteAllTextAsync(PathFor(), System.Text.Json.JsonSerializer.Serialize(batches));

    private static string PathFor()
        => Path.Combine(FileSystem.AppDataDirectory, FileName);
}

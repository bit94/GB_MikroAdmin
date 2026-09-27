using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Conserva los lotes generados. Sin esto, cerrar la aplicación perdía los códigos recién
/// creados aunque siguieran activos en el router, y no había forma de reimprimirlos.
/// </summary>
public interface ITicketHistoryStore
{
    Task SaveAsync(TicketBatchExport batch);
    Task<IReadOnlyList<TicketBatchExport>> GetAllAsync();
    Task RemoveAsync(TicketBatchExport batch);

    /// <summary>Registra códigos del lote como ya impresos, uniendo con los que ya constaban.</summary>
    Task MarkPrintedAsync(TicketBatchExport batch, IReadOnlyCollection<string> codes);
}

/// <summary>Persistencia en el almacén local del dispositivo, en JSON.</summary>
public sealed class TicketHistoryStore : ITicketHistoryStore
{
    private const string FileName = "ticket-history.json";
    private const string LegacyFileName = "voucher-history.json";
    private const int MaxBatches = 100;

    public async Task SaveAsync(TicketBatchExport batch)
    {
        var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
        all.Insert(0, batch);
        if (all.Count > MaxBatches)
            all.RemoveRange(MaxBatches, all.Count - MaxBatches);

        await WriteAsync(all);
    }

    public Task<IReadOnlyList<TicketBatchExport>> GetAllAsync() => ReadAsync();

    public async Task RemoveAsync(TicketBatchExport batch)
    {
        // Se identifica por marca de tiempo: dos lotes no se crean en el mismo instante.
        var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
        await WriteAsync(all);
    }

    public async Task MarkPrintedAsync(TicketBatchExport batch, IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0)
            return;

        var all = await ReadAsync();
        var existing = all.FirstOrDefault(b => b.CreatedAt == batch.CreatedAt);
        if (existing is null)
            return;

        var merged = existing.Printed.Union(codes).ToList();
        if (merged.Count == existing.Printed.Count)
            return;

        await SaveAsync(existing with { Printed = merged });
    }

    private async Task<IReadOnlyList<TicketBatchExport>> ReadAsync()
    {
        var path = PathFor(FileName);
        if (!File.Exists(path))
        {
            // Los lotes guardados antes del renombre a "tickets" siguen en el archivo antiguo;
            // se leen desde ahí y la siguiente escritura ya crea el archivo nuevo.
            path = PathFor(LegacyFileName);
            if (!File.Exists(path))
                return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return System.Text.Json.JsonSerializer.Deserialize<List<TicketBatchExport>>(json) ?? [];
        }
        catch (Exception)
        {
            // Un histórico ilegible no debe impedir usar la aplicación.
            return [];
        }
    }

    private static async Task WriteAsync(IReadOnlyList<TicketBatchExport> batches)
        => await File.WriteAllTextAsync(PathFor(FileName), System.Text.Json.JsonSerializer.Serialize(batches));

    private static string PathFor(string fileName)
        => Path.Combine(FileSystem.AppDataDirectory, fileName);
}

using System.Text;
using System.Text.Json;

namespace GB.MikroAdmin.Core.Services;

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

/// <summary>
/// Persistencia en JSON dentro de un directorio de datos. Vive en Core para poder probarla sin
/// MAUI; la aplicación le pasa <c>FileSystem.AppDataDirectory</c>.
/// </summary>
/// <remarks>
/// La lectura de un archivo dañado ya no se traduce en una lista vacía: eso hacía que el
/// siguiente guardado sobrescribiera el histórico completo con un solo lote. Ahora se intenta
/// recuperar la copia anterior y, si tampoco sirve, se lanza un error sin tocar los archivos.
/// </remarks>
public sealed class TicketHistoryStore : ITicketHistoryStore
{
    internal const string FileName = "ticket-history.json";
    internal const string LegacyFileName = "voucher-history.json";
    internal const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";
    private const int MaxBatches = 100;

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TicketHistoryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task SaveAsync(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await _gate.WaitAsync();
        try
        {
            var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
            all.Insert(0, batch);
            if (all.Count > MaxBatches)
                all.RemoveRange(MaxBatches, all.Count - MaxBatches);

            await WriteAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TicketBatchExport>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(TicketBatchExport batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await _gate.WaitAsync();
        try
        {
            // Se identifica por marca de tiempo: dos lotes no se crean en el mismo instante.
            var all = (await ReadAsync()).Where(b => b.CreatedAt != batch.CreatedAt).ToList();
            await WriteAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkPrintedAsync(TicketBatchExport batch, IReadOnlyCollection<string> codes)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(codes);

        if (codes.Count == 0)
            return;

        await _gate.WaitAsync();
        try
        {
            var all = await ReadAsync();
            var index = all.FindIndex(b => b.CreatedAt == batch.CreatedAt);
            if (index < 0)
                return;

            var merged = all[index].Printed.Union(codes).ToList();
            if (merged.Count == all[index].Printed.Count)
                return;

            all[index] = all[index] with { Printed = merged };
            await WriteAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<TicketBatchExport>> ReadAsync()
    {
        var main = PathFor(FileName);
        if (File.Exists(main))
        {
            if (await TryReadAsync(main) is { } loaded)
                return loaded;

            var backup = main + BackupSuffix;
            if (File.Exists(backup) && await TryReadAsync(backup) is { } recovered)
                return recovered;

            throw new InvalidDataException(
                $"El histórico «{main}» está dañado y su respaldo no es válido. " +
                "Mueve o corrige el archivo antes de volver a guardar; no se ha sobrescrito nada.");
        }

        // Los lotes guardados antes del renombre a "tickets" siguen en el archivo antiguo;
        // la siguiente escritura crea ya el archivo nuevo.
        var legacy = PathFor(LegacyFileName);
        if (File.Exists(legacy))
        {
            if (await TryReadAsync(legacy) is { } loaded)
                return loaded;

            throw new InvalidDataException(
                $"El histórico anterior «{legacy}» está dañado. " +
                "Mueve o corrige el archivo antes de volver a guardar; no se ha sobrescrito nada.");
        }

        return [];
    }

    /// <summary>Devuelve null solo si el contenido no es JSON válido del histórico.</summary>
    private static async Task<List<TicketBatchExport>?> TryReadAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<TicketBatchExport>>(stream) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task WriteAsync(IReadOnlyList<TicketBatchExport> batches)
    {
        Directory.CreateDirectory(_directory);

        var path = PathFor(FileName);
        var temp = path + TempSuffix;
        var backup = path + BackupSuffix;

        try
        {
            // El archivo temporal se escribe y se fuerza a disco antes de reemplazar el bueno:
            // un corte a mitad deja intacto el histórico anterior.
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                await writer.WriteAsync(JsonSerializer.Serialize(batches));
                await writer.FlushAsync();
                stream.Flush(flushToDisk: true);
            }

            // Solo se respalda un principal que todavía se puede leer. Si la lectura anterior
            // tuvo que recuperarse del respaldo, copiar el principal dañado encima destruiría
            // la única copia buena justo antes de reemplazarlo.
            if (File.Exists(path) && await TryReadAsync(path) is not null)
                File.Copy(path, backup, overwrite: true);

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string PathFor(string fileName) => Path.Combine(_directory, fileName);
}

using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.Services;

/// <summary>
/// Entrega al usuario un lote exportado. Se declara como interfaz para que los ViewModels no
/// dependan de MAUI ni del sistema de archivos, y las pruebas puedan sustituirlo.
/// </summary>
public interface IFileExporter
{
    /// <summary>Escribe el contenido y abre el diálogo de guardado del sistema.</summary>
    Task ShareTextAsync(string suggestedFileName, string content, string title);

    /// <summary>Escribe los bytes y abre el diálogo de guardado, o los comparte si no aplica.</summary>
    Task ShareBytesAsync(string suggestedFileName, byte[] content, string mimeType, string title);

    /// <summary>Envía el PDF directamente al diálogo de impresión del sistema.</summary>
    Task PrintAsync(string suggestedFileName, byte[] pdf, string jobName);
}

/// <summary>Implementación sobre las API de MAUI.</summary>
public sealed class MauiFileExporter : IFileExporter
{
    public async Task ShareTextAsync(string suggestedFileName, string content, string title)
    {
        var path = await WriteToCacheAsync(suggestedFileName, System.Text.Encoding.UTF8.GetBytes(content));
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path)
        });
    }

    public async Task ShareBytesAsync(string suggestedFileName, byte[] content, string mimeType, string title)
    {
        var path = await WriteToCacheAsync(suggestedFileName, content);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path, mimeType)
        });
    }

    /// <summary>
    /// Abre el PDF con la aplicación predeterminada. Desde ahí el usuario elige imprimir: MAUI no
    /// expone un diálogo de impresión propio, y abrirlo en el visor del sistema es el camino que
    /// funciona en Android y en Windows sin dependencias adicionales.
    /// </summary>
    public async Task PrintAsync(string suggestedFileName, byte[] pdf, string jobName)
    {
        var path = await WriteToCacheAsync(suggestedFileName, pdf);
        await Launcher.Default.OpenAsync(new OpenFileRequest(jobName, new ReadOnlyFile(path)));
    }

    /// <summary>
    /// Los archivos se escriben en la caché y no en el almacén de datos: son entregables
    /// temporales que el sistema puede limpiar, no documentación que haya que conservar.
    /// </summary>
    private static async Task<string> WriteToCacheAsync(string fileName, byte[] content)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, Sanitize(fileName));
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    /// <summary>Evita separadores de ruta y caracteres prohibidos en el nombre del archivo.</summary>
    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "tickets" : cleaned;
    }
}

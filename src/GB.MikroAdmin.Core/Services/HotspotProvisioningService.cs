using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

public record CreatedHotspotUser(string Name, string Password);

/// <summary>Avance de un lote en curso, para poder mostrarlo en pantalla.</summary>
public readonly record struct BatchProgress(int Completed, int Total)
{
    public int Remaining => Total - Completed;

    public double Percent => Total <= 0 ? 0 : (double)Completed / Total * 100;
}

public interface IHotspotProvisioningService
{
    Task<IReadOnlyList<CreatedHotspotUser>> CreateBatchAsync(
        int quantity,
        string profile,
        TimeSpan limit,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Genera vouchers donde el código visible es a la vez usuario y contraseña, que es lo que
/// permite entregar un solo dato al cliente.
/// </summary>
public sealed class HotspotProvisioningService : IHotspotProvisioningService
{
    public const int MinQuantity = 1;
    public const int MaxQuantity = 100;
    /// <summary>Reintentos ante un código que ya existe en el router antes de rendirse.</summary>
    public const int CodeAttempts = 5;

    private readonly IRouterUserWriter _router;
    private readonly IVoucherCodeGenerator _codes;

    public HotspotProvisioningService(IRouterUserWriter router, IVoucherCodeGenerator codes)
    {
        _router = router;
        _codes = codes;
    }

    public async Task<IReadOnlyList<CreatedHotspotUser>> CreateBatchAsync(
        int quantity,
        string profile,
        TimeSpan limit,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity is < MinQuantity or > MaxQuantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"La cantidad debe estar entre {MinQuantity} y {MaxQuantity}.");
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("Selecciona un perfil.", nameof(profile));
        if (limit <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "La duración del voucher debe ser mayor que cero.");

        // Los códigos se acuñan primero para poder detectar un duplicado dentro del mismo lote
        // sin haber creado ya parte de los usuarios en el router.
        var codes = CoiningCodes(quantity);

        var created = new List<CreatedHotspotUser>(quantity);
        var failed = new List<string>();

        progress?.Report(new BatchProgress(0, codes.Count));

        for (var index = 0; index < codes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var code = codes[index];
            try
            {
                await _router.AddUserAsync(code, code, profile, limit, cancellationToken);
                created.Add(new CreatedHotspotUser(code, code));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed.Add(code);
            }

            // Se reporta también el avance de los fallidos: de lo contrario la barra se quedaría
            // corta para siempre cuando el router rechaza alguno.
            progress?.Report(new BatchProgress(index + 1, codes.Count));
        }

        if (failed.Count > 0)
            throw new VoucherBatchException(created, failed);

        return created;
    }

    private List<string> CoiningCodes(int quantity)
    {
        var codes = new HashSet<string>(quantity, StringComparer.OrdinalIgnoreCase);
        var guard = 0;
        var limit = quantity * CodeAttempts;

        while (codes.Count < quantity)
        {
            if (++guard > limit)
                throw new InvalidOperationException("No se pudieron generar códigos únicos suficientes; reduce la cantidad.");
            codes.Add(_codes.Next());
        }

        return [.. codes];
    }
}

/// <summary>Se lanzó parte del lote y falló el resto; conserva lo creado para no perder los códigos ya válidos.</summary>
public sealed class VoucherBatchException(IReadOnlyList<CreatedHotspotUser> created, IReadOnlyList<string> failed)
    : Exception($"Se crearon {created.Count} vouchers y fallaron {failed.Count} ({string.Join(", ", failed)}).")
{
    public IReadOnlyList<CreatedHotspotUser> Created { get; } = created;
    public IReadOnlyList<string> Failed { get; } = failed;
}

public interface IVoucherCodeGenerator
{
    string Next();
}

/// <summary>Código numérico de 6 dígitos, igual que el que se dicta en mostrador.</summary>
public sealed class NumericVoucherCodeGenerator : IVoucherCodeGenerator
{
    public const int MinInclusive = 100_000;
    public const int MaxExclusive = 1_000_000;

    public string Next() => System.Security.Cryptography.RandomNumberGenerator
        .GetInt32(MinInclusive, MaxExclusive)
        .ToString();
}

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
/// Genera tickets donde el código visible es a la vez usuario y contraseña, que es lo que
/// permite entregar un solo dato al cliente.
/// </summary>
public sealed class HotspotProvisioningService : IHotspotProvisioningService
{
    public const int MinQuantity = 1;
    public const int MaxQuantity = 100;
    /// <summary>Tope de intentos al acuñar códigos únicos para el lote; no reintenta los que ya existan en el router, que pasan a la lista de fallidos.</summary>
    public const int CodeAttempts = 5;

    private readonly IRouterUserWriter _router;
    private readonly ITicketCodeGenerator _codes;

    public HotspotProvisioningService(IRouterUserWriter router, ITicketCodeGenerator codes)
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
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "La duración del ticket debe ser mayor que cero.");

        // Los códigos se acuñan primero para poder detectar un duplicado dentro del mismo lote
        // sin haber creado ya parte de los usuarios en el router.
        var codes = new Queue<string>(CoiningCodes(quantity));
        var seen = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

        var created = new List<CreatedHotspotUser>(quantity);
        var failed = new List<string>();
        var resolved = 0;
        var notAttempted = 0;
        string? abortReason = null;

        progress?.Report(new BatchProgress(0, quantity));

        while (resolved < quantity)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var code = codes.Dequeue();
            var retries = 0;

            while (true)
            {
                try
                {
                    await _router.AddUserAsync(code, code, profile, limit, cancellationToken);
                    created.Add(new CreatedHotspotUser(code, code));
                    resolved++;
                    break;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var (kind, reason) = Describe(exception);

                    // Un código que ya existe en el router es una colisión, no un fallo del
                    // lote: se prueba otro código antes de darlo por perdido.
                    if (kind is RouterWriteFailureKind.Duplicate
                        && retries < CodeAttempts
                        && TryMintCode(seen) is { } replacement)
                    {
                        retries++;
                        code = replacement;
                        continue;
                    }

                    failed.Add(code);

                    if (kind is RouterWriteFailureKind.Duplicate)
                    {
                        resolved++;
                        break;
                    }

                    // Conexión, credenciales, permisos o configuración afectan a todos los
                    // tickets por igual: seguir intentando solo multiplica la espera.
                    abortReason = reason;
                    notAttempted = quantity - resolved - failed.Count;
                    resolved = quantity;
                    break;
                }
            }

            // Se reporta también el avance de los fallidos: de lo contrario la barra se quedaría
            // corta para siempre cuando el router rechaza alguno.
            progress?.Report(new BatchProgress(resolved, quantity));
        }

        if (failed.Count > 0)
        {
            throw new TicketBatchException(
                created, failed,
                abortReason ?? "El código ya existía en el router y no se encontró otro libre.",
                notAttempted);
        }

        return created;
    }

    private static (RouterWriteFailureKind Kind, string Reason) Describe(Exception exception)
        => exception is RouterWriteException known
            ? (known.Kind, known.Message)
            : (RouterWriteFailureKind.Router,
               string.IsNullOrWhiteSpace(exception.Message) ? "Error inesperado del router." : exception.Message);

    private string? TryMintCode(HashSet<string> seen)
    {
        for (var attempt = 0; attempt < CodeAttempts; attempt++)
        {
            var code = _codes.Next();
            if (seen.Add(code))
                return code;
        }

        return null;
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
public sealed class TicketBatchException(
    IReadOnlyList<CreatedHotspotUser> created,
    IReadOnlyList<string> failed,
    string reason,
    int notAttempted = 0)
    : Exception(BuildMessage(created, failed, reason, notAttempted))
{
    public IReadOnlyList<CreatedHotspotUser> Created { get; } = created;
    public IReadOnlyList<string> Failed { get; } = failed;

    /// <summary>Motivo en lenguaje claro del primer fallo que detuvo el lote.</summary>
    public string Reason { get; } = reason;

    /// <summary>Tickets que no llegaron a intentarse porque el lote se detuvo.</summary>
    public int NotAttempted { get; } = notAttempted;

    private static string BuildMessage(
        IReadOnlyList<CreatedHotspotUser> created,
        IReadOnlyList<string> failed,
        string reason,
        int notAttempted)
        => notAttempted > 0
            ? $"Se crearon {created.Count} tickets. El lote se detuvo: {reason} " +
              $"Quedaron {notAttempted} sin intentar."
            : $"Se crearon {created.Count} tickets y fallaron {failed.Count} " +
              $"({string.Join(", ", failed)}). Motivo: {reason}";
}

public interface ITicketCodeGenerator
{
    string Next();
}

/// <summary>Código numérico de 6 dígitos, igual que el que se dicta en mostrador.</summary>
public sealed class NumericTicketCodeGenerator : ITicketCodeGenerator
{
    public const int MinInclusive = 100_000;
    public const int MaxExclusive = 1_000_000;

    public string Next() => System.Security.Cryptography.RandomNumberGenerator
        .GetInt32(MinInclusive, MaxExclusive)
        .ToString();
}

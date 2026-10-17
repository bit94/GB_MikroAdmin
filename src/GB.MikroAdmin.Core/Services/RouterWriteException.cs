namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Clasificación de un fallo al escribir en el router. Permite distinguir una colisión de
/// código (se puede reintentar) de una caída de conexión o de permisos (afecta a todo el lote).
/// </summary>
public enum RouterWriteFailureKind
{
    Duplicate,
    Connection,
    Authentication,
    Permission,
    Router
}

/// <summary>Fallo de escritura ya clasificado, independiente de la librería que habla con RouterOS.</summary>
public sealed class RouterWriteException(
    RouterWriteFailureKind kind,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public RouterWriteFailureKind Kind { get; } = kind;
}

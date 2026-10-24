using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.Services;

/// <summary>
/// Aviso de tickets agotados. La implementación es por plataforma: Android usa una notificación
/// con canal y permiso propio; Windows usa una notificación de la Windows App SDK.
/// </summary>
public sealed partial class TicketNotifier : ITicketNotifier
{
}

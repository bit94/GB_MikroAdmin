using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Consulta los tickets mientras la aplicación está conectada y muestra un aviso del sistema
/// cuando alguno agota su tiempo. RouterOS no notifica vencimientos por sí solo, y sin un
/// servidor no hay push real: el aviso nace en el dispositivo que tiene la sesión.
/// </summary>
public sealed class TicketExpiryMonitor : IDisposable
{
    private readonly ITicketSource _source;
    private readonly ITicketNotifier _notifier;
    private readonly TicketExpiryTracker _tracker;
    private readonly TimeSpan _interval;

    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private bool _notified;

    public TicketExpiryMonitor(ITicketSource source, ITicketNotifier notifier, TicketExpiryTracker tracker, TimeSpan interval)
    {
        _source = source;
        _notifier = notifier;
        _tracker = tracker;
        _interval = interval;
    }

    /// <summary>Arranca la vigilancia una sola vez; llamarlo de nuevo no duplica el sondeo.</summary>
    public void Start()
    {
        if (_loop is { IsCompleted: false })
            return;

        _cancellation = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public async Task StopAsync()
    {
        var cancellation = _cancellation;
        var loop = _loop;
        _cancellation = null;
        _loop = null;

        if (cancellation is null)
            return;

        cancellation.Cancel();
        try
        {
            if (loop is not null)
                await loop;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
            _tracker.Reset();
            _notified = false;
        }

        // Al desconectar, el aviso deja de ser accionable: la pantalla de Tickets ya no tiene sesión.
        try
        {
            await _notifier.ClearExpiredAsync();
        }
        catch
        {
            // Sin notificaciones la app sigue funcionando con normalidad.
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.EnsurePermissionAsync();
        }
        catch
        {
            // Si el usuario rechaza el permiso, la vigilancia sigue sin avisos del sistema.
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Un corte momentáneo del router no debe detener la vigilancia.
            }

            try
            {
                await Task.Delay(_interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Un solo sondeo, separado del bucle para poder probarlo sin esperas.</summary>
    internal async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_source.IsConnected)
            return;

        var users = await _source.GetUsersAsync(cancellationToken);
        var expired = _tracker.Observe(users);

        if (expired.Count > 0)
        {
            await _notifier.NotifyExpiredAsync(expired);
            _notified = true;
        }
        else if (_notified && !users.Any(user => user.State == TicketState.Expired))
        {
            // El administrador ya limpió los agotados: la notificación anterior quedó obsoleta.
            await _notifier.ClearExpiredAsync();
            _notified = false;
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _loop = null;
    }
}

using System.Collections.ObjectModel;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

public enum NoticeSeverity { Info, Success, Warning, Error }

public sealed record Notice(string Text, NoticeSeverity Severity)
{
    public bool IsError => Severity is NoticeSeverity.Error;
}

/// <summary>
/// Estado de la sesión con el router, compartido por todas las páginas. Antes cada página
/// ignoraba a qué equipo estaba conectada, lo que en un uso diario con varios routers es
/// exactamente el dato que más se necesita tener a la vista.
/// </summary>
public sealed class ConnectionState : ObservableObject
{
    private readonly IRouterService _router;

    public ConnectionState(IRouterService router) => _router = router;

    public string? Host => _router.ConnectedHost;

    public bool IsConnected => _router.IsConnected;

    /// <summary>Descripción corta para la barra superior: "192.168.88.1:8729 (simulado)".</summary>
    public string Display => _router.ConnectedHost ?? "Sin conexión";

    public async Task DisconnectAsync()
    {
        await _router.DisconnectAsync();
        Raise(nameof(Host));
        Raise(nameof(IsConnected));
        Raise(nameof(Display));
    }

    public void Refresh()
    {
        Raise(nameof(Host));
        Raise(nameof(IsConnected));
        Raise(nameof(Display));
    }
}

/// <summary>
/// Base de los ViewModels de página: mantiene las notificaciones, el estado de carga y el
/// error de la última operación. Antes esto era una única propiedad <c>Message</c> sin
/// severidad que se sobreescribía sin dejar rastro.
/// </summary>
public abstract class PageViewModel : ObservableObject
{
    private bool _isBusy;

    protected PageViewModel(ConnectionState connection) => Connection = connection;

    public ConnectionState Connection { get; }

    public ObservableCollection<Notice> Notices { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
                Raise(nameof(IsIdle));
        }
    }

    public bool IsIdle => !_isBusy;

    protected void Info(string text) => Notify(text, NoticeSeverity.Info);
    protected void Success(string text) => Notify(text, NoticeSeverity.Success);
    protected void Warn(string text) => Notify(text, NoticeSeverity.Warning);
    protected void Fail(string text) => Notify(text, NoticeSeverity.Error);

    private void Notify(string text, NoticeSeverity severity)
    {
        Notices.Insert(0, new Notice(text, severity));
        // Las notificaciones se muestran junto a la acción; se conservan las últimas para que
        // el usuario pueda releer un error sin que desaparezca al siguiente mensaje.
        while (Notices.Count > 20)
            Notices.RemoveAt(Notices.Count - 1);
    }

    /// <summary>Ejecuta una operación de página controlando la carga y traduciendo el error a un aviso.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Warn("Operación cancelada.");
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

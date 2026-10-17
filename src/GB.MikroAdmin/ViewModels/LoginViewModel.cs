using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Views;

namespace GB.MikroAdmin.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly IRouterSettingsStore _settings;
    private readonly IRouterService _router;
    private readonly ConnectionState _connection;

    private string _host = "192.168.88.1";
    private string _port = "8729";
    private string _userName = "admin";
    private string _password = string.Empty;
    private string _message = string.Empty;
    private bool _simulated = true;
    private bool _useTls = true;
    private bool _isConnecting;

    public LoginViewModel(IRouterSettingsStore settings, IRouterService router, ConnectionState connection)
    {
        _settings = settings;
        _router = router;
        _connection = connection;
        ConnectCommand = new AsyncCommand(ConnectAsync);
    }

    public string Host { get => _host; set => Set(ref _host, value); }
    public string Port { get => _port; set => Set(ref _port, value); }
    public string UserName { get => _userName; set => Set(ref _userName, value); }
    public string Password { get => _password; set => Set(ref _password, value); }
    public string Message { get => _message; set => Set(ref _message, value); }

    public bool Simulated
    {
        get => _simulated;
        set
        {
            if (Set(ref _simulated, value))
            {
                // En simulado no hay TLS ni credenciales que valgan; se refleja para no confundir.
                if (value)
                    UseTls = false;
                Raise(nameof(RequiresCredentials));
            }
        }
    }

    public bool UseTls
    {
        get => _useTls;
        set
        {
            if (Set(ref _useTls, value))
                Raise(nameof(RequiresCredentials));
        }
    }

    /// <summary>En modo simulado no hace falta usuario ni contraseña reales.</summary>
    public bool RequiresCredentials => !Simulated;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => Set(ref _isConnecting, value);
    }

    public ICommand ConnectCommand { get; }

    public async Task RestoreAsync()
    {
        try
        {
            var saved = await _settings.GetAsync();
            if (saved is null)
                return;

            // Si ya hay una contraseña escrita (por ejemplo, tras un intento fallido), no se
            // pisan los datos que el usuario está corrigiendo. Al volver tras desconectar el
            // campo está vacío y se recupera la configuración guardada.
            if (!string.IsNullOrEmpty(Password))
                return;

            Host = saved.Host;
            Port = saved.Port.ToString();
            UserName = saved.UserName;
            Simulated = saved.Simulated;
            UseTls = saved.UseTls;

            // Se rellena para no pedirla en cada arranque. El campo está enmascarado y la copia
            // persistente sigue siendo la de SecureStorage; nunca se escribe en claro.
            Password = saved.Simulated ? string.Empty : saved.Password;
        }
        catch (Exception exception)
        {
            // Si el almacén seguro falla, se puede reintentar al volver a la pantalla.
            Message = $"No se pudo recuperar la conexión guardada: {exception.Message}";
        }
    }

    private async Task ConnectAsync()
    {
        if (IsConnecting)
            return;

        IsConnecting = true;
        Message = string.Empty;
        try
        {
            if (!int.TryParse(Port, out var port) || port is < 1 or > 65535)
                throw new ArgumentException("El puerto debe estar entre 1 y 65535.");
            if (!Simulated && string.IsNullOrWhiteSpace(UserName))
                throw new ArgumentException("Indica el usuario del router.");

            // Se guarda antes de conectar para que el servicio lea la misma configuración.
            await _settings.SaveAsync(new RouterSettings(
                Host.Trim(), port, UserName.Trim(), Password,
                Simulated ? false : UseTls, Simulated));

            await _router.ConnectAsync();
            _connection.Refresh();

            Password = string.Empty;
            await Shell.Current.GoToAsync(Routes.Home);
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsConnecting = false;
        }
    }
}

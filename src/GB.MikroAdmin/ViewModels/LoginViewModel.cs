using GB.MikroAdmin.Models;
using GB.MikroAdmin.Services;
using System.Windows.Input;
namespace GB.MikroAdmin.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    readonly IRouterSettingsStore _settings; readonly IRouterService _router; string _host = "192.168.88.1", _port = "8729", _user = "admin", _password = "", _message = ""; bool _simulated = true, _tls = true;
    public string Host { get => _host; set => Set(ref _host, value); }
    public string Port { get => _port; set => Set(ref _port, value); }
    public string UserName { get => _user; set => Set(ref _user, value); }
    public string Password { get => _password; set => Set(ref _password, value); }
    public bool Simulated { get => _simulated; set => Set(ref _simulated, value); }
    public bool UseTls { get => _tls; set => Set(ref _tls, value); }
    public string Message { get => _message; set => Set(ref _message, value); }
    public ICommand ConnectCommand { get; }
    public LoginViewModel(IRouterSettingsStore settings, IRouterService router) { _settings = settings; _router = router; ConnectCommand = new AsyncCommand(ConnectAsync); }
    async Task ConnectAsync() { try { if (!int.TryParse(Port, out var port)) throw new ArgumentException("Puerto inválido."); await _settings.SaveAsync(new(Host, port, UserName, Password, UseTls, Simulated)); await _router.ConnectAsync(); Password = ""; await Shell.Current.GoToAsync("//home"); } catch (Exception ex) { Message = ex.Message; } }
}

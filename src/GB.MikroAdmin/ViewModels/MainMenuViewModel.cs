using System.Windows.Input;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Views;

namespace GB.MikroAdmin.ViewModels;

public sealed class MainMenuViewModel(ConnectionState connection, TicketExpiryMonitor monitor) : PageViewModel(connection)
{
    public ICommand GoToInterfacesCommand { get; } = new AsyncCommand(() => Shell.Current.GoToAsync(Routes.Interfaces));
    public ICommand GoToUsersCommand { get; } = new AsyncCommand(() => Shell.Current.GoToAsync(Routes.Users));
    public ICommand GoToGenerateCommand { get; } = new AsyncCommand(() => Shell.Current.GoToAsync(Routes.Generate));
    public ICommand GoToProfilesCommand { get; } = new AsyncCommand(() => Shell.Current.GoToAsync(Routes.Profiles));
    public ICommand GoToHistoryCommand { get; } = new AsyncCommand(() => Shell.Current.GoToAsync(Routes.History));

    public ICommand DisconnectCommand { get; } = new AsyncCommand(async () =>
    {
        // Primero se detiene la vigilancia para que no consulte el router mientras se cierra.
        await monitor.StopAsync();
        await connection.DisconnectAsync();
        await Shell.Current.GoToAsync(Routes.LoginRoot);
    });
}

using System.Windows.Input;

namespace GB.MikroAdmin.ViewModels;

public sealed class MainMenuViewModel : ObservableObject
{
    public MainMenuViewModel()
    {
        GoToInterfacesCommand = new AsyncCommand(() => NavigateAsync("interfaces"));
        GoToUsersCommand = new AsyncCommand(() => NavigateAsync("users"));
        GoToProfilesCommand = new AsyncCommand(() => NavigateAsync("profiles"));
    }

    public ICommand GoToInterfacesCommand { get; }
    public ICommand GoToUsersCommand { get; }
    public ICommand GoToProfilesCommand { get; }

    private static Task NavigateAsync(string route) => Shell.Current.GoToAsync($"//{route}");
}

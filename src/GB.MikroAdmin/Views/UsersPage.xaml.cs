using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class UsersPage : ContentPage
{
    private readonly UsersViewModel _viewModel;
    private IDispatcherTimer? _timer;

    public UsersPage(UsersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
        StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAutoRefresh();
    }

    /// <summary>
    /// Refresca sola cada 30 s. El tiempo restante de un ticket cambia constantemente y antes
    /// solo se actualizaba al pulsar "Actualizar", así que la pantalla quedaba desactualizada.
    /// </summary>
    private void StartAutoRefresh()
    {
        if (_timer is not null)
            return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick += async (_, _) =>
        {
            if (_viewModel.IsIdle)
                await _viewModel.RefreshAsync();
        };
        _timer.Start();
    }

    private void StopAutoRefresh()
    {
        _timer?.Stop();
        _timer = null;
    }
}

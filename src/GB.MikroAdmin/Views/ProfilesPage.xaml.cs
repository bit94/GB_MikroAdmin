using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class ProfilesPage : ContentPage
{
    private readonly ProfilesViewModel _viewModel;

    public ProfilesPage(ProfilesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}

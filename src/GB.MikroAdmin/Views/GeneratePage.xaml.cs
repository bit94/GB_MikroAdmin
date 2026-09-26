using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class GeneratePage : ContentPage
{
    private readonly UsersViewModel _viewModel;

    public GeneratePage(UsersViewModel viewModel)
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

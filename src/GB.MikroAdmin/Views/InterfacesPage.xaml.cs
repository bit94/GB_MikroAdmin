using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class InterfacesPage : ContentPage
{
    private readonly InterfacesViewModel _viewModel;

    public InterfacesPage(InterfacesViewModel viewModel)
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

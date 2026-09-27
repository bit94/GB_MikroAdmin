using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class VoucherHistoryPage : ContentPage
{
    private readonly VoucherHistoryViewModel _viewModel;

    public VoucherHistoryPage(VoucherHistoryViewModel viewModel)
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

using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class TicketHistoryPage : ContentPage
{
    private readonly TicketHistoryViewModel _viewModel;

    public TicketHistoryPage(TicketHistoryViewModel viewModel)
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

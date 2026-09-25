using GB.MikroAdmin.ViewModels;
namespace GB.MikroAdmin.Views; public partial class DashboardPage : ContentPage { readonly DashboardViewModel _vm; public DashboardPage(DashboardViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; } protected override async void OnAppearing() { base.OnAppearing(); await _vm.RefreshAsync(); } }

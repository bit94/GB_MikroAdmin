using GB.MikroAdmin.ViewModels;
namespace GB.MikroAdmin.Views; public partial class LoginPage : ContentPage { public LoginPage(LoginViewModel vm) { InitializeComponent(); BindingContext = vm; } }

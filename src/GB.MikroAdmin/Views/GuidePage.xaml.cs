using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class GuidePage : ContentPage
{
    public GuidePage(GuideViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

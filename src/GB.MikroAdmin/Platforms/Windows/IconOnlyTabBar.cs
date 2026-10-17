using Microsoft.UI.Xaml.Markup;
using WApplication = Microsoft.UI.Xaml.Application;
using WDataTemplate = Microsoft.UI.Xaml.DataTemplate;

namespace GB.MikroAdmin.Platforms.Windows;

/// <summary>
/// Sustituye la plantilla de las pestañas de Shell para mostrar solo el icono. El título se
/// conserva en la información accesible y en el tooltip, de modo que la barra sigue siendo
/// navegable con lector de pantalla y muestra el nombre al pasar el puntero.
/// </summary>
internal static class IconOnlyTabBar
{
    private const string Template = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <NavigationViewItem
                Background="{Binding Background}"
                IsEnabled="{Binding IsEnabled}"
                IsSelected="{Binding IsSelected, Mode=TwoWay}"
                MenuItemsSource="{Binding MenuItemsSource}"
                Icon="{Binding Icon}"
                AutomationProperties.Name="{Binding Content}"
                ToolTipService.ToolTip="{Binding Content}"
                HorizontalContentAlignment="Center"
                MinWidth="56" />
        </DataTemplate>
        """;

    public static void Apply()
    {
        if (WApplication.Current is not { } application)
            return;

        application.Resources["TabBarNavigationViewMenuItem"] =
            (WDataTemplate)XamlReader.Load(Template);
    }
}

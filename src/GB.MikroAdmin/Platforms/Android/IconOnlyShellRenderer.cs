using Android.Content;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace GB.MikroAdmin.Platforms.Android;

/// <summary>
/// Muestra solo iconos en la barra inferior de Shell. Los títulos siguen en el menú de cada
/// elemento, así que TalkBack y la hoja «Más» conservan sus nombres.
/// </summary>
public sealed class IconOnlyShellRenderer : ShellRenderer
{
    public IconOnlyShellRenderer(Context context) : base(context)
    {
    }

    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new IconOnlyBottomNavViewAppearanceTracker(this, shellItem);
}

file sealed class IconOnlyBottomNavViewAppearanceTracker(IShellContext context, ShellItem shellItem)
    : ShellBottomNavViewAppearanceTracker(context, shellItem)
{
    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityUnlabeled;
    }

    public override void ResetAppearance(BottomNavigationView bottomView)
    {
        base.ResetAppearance(bottomView);
        bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityUnlabeled;
    }
}

using System.Globalization;
using GB.MikroAdmin.Core.Models;

namespace GB.MikroAdmin.Views;

/// <summary>
/// Traduce el estado del voucher a texto y color. Se centraliza aquí para que la semántica
/// (verde disponible, ámbar en uso, rojo agotado) sea idéntica en todas las listas.
/// </summary>
public sealed class VoucherStateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is VoucherState state
            ? state switch
            {
                VoucherState.Available => "Sin usar",
                VoucherState.InUse => "En uso",
                VoucherState.Expired => "Agotado",
                _ => "Sin límite"
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class VoucherStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is VoucherState state
            ? state switch
            {
                VoucherState.Available => Color.FromArgb("#22C55E"),
                VoucherState.InUse => Color.FromArgb("#F59E0B"),
                VoucherState.Expired => Color.FromArgb("#EF4444"),
                _ => Color.FromArgb("#64748B")
            }
            : Color.FromArgb("#64748B");

        return color;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Muestra la duración restante en lenguaje corriente, o "—" si no hay límite.</summary>
public sealed class RemainingTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan remaining)
            return "Sin límite";
        if (remaining <= TimeSpan.Zero)
            return "Agotado";

        var t = remaining;
        return t.TotalDays >= 1
            ? $"{(int)t.TotalDays}d {t.Hours}h"
            : t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes}m"
                : $"{t.Minutes}m {t.Seconds}s";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Invierte un booleano, para habilitar un botón mientras otro estado está activo.</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>Verde si la interfaz está activa, gris si no.</summary>
public sealed class RunningColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.FromArgb("#22C55E") : Color.FromArgb("#64748B");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Color de la severidad de un aviso.</summary>
public sealed class NoticeColorConverter : IValueConverter
{    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ViewModels.NoticeSeverity severity
            ? severity switch
            {
                ViewModels.NoticeSeverity.Success => Color.FromArgb("#22C55E"),
                ViewModels.NoticeSeverity.Warning => Color.FromArgb("#F59E0B"),
                ViewModels.NoticeSeverity.Error => Color.FromArgb("#EF4444"),
                _ => Color.FromArgb("#94A3B8")
            }
            : Color.FromArgb("#94A3B8");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.Globalization;

namespace GB.MikroAdmin.Core.Services;

/// <summary>
/// Conversión entre <see cref="TimeSpan"/> y la notación de duración de RouterOS
/// (<c>1w2d3h4m5s</c>). Aislado aquí para poder probarlo sin router.
/// </summary>
public static class RouterOsTime
{
    /// <summary>Convierte una duración al formato que acepta RouterOS en <c>limit-uptime</c>.</summary>
    /// <remarks>
    /// RouterOS no acepta que un único componente desborde su unidad: <c>168h</c> no es
    /// equivalente a <c>7d</c>. Por eso se descompone en semanas, días, horas, minutos y
    /// segundos en lugar de emitir las horas totales.
    /// </remarks>
    public static string ToRouterOs(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), value, "La duración debe ser mayor que cero.");

        var sb = new System.Text.StringBuilder();
        var days = value.Days;
        var weeks = days / 7;
        days %= 7;

        if (weeks > 0) sb.Append(weeks).Append('w');
        if (days > 0) sb.Append(days).Append('d');
        if (value.Hours > 0) sb.Append(value.Hours).Append('h');
        if (value.Minutes > 0) sb.Append(value.Minutes).Append('m');
        if (value.Seconds > 0) sb.Append(value.Seconds).Append('s');

        return sb.ToString();
    }

    /// <summary>
    /// Interpreta una duración de RouterOS. Devuelve <c>null</c> cuando el valor está vacío
    /// y lanza <see cref="FormatException"/> cuando el texto no es una duración válida.
    /// </summary>
    /// <remarks>
    /// Falla de forma explícita a propósito: antes un carácter desconocido se traducía a
    /// cero y un ticket con límite ilegible acababa pareciendo ilimitado.
    /// </remarks>
    public static TimeSpan? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var total = TimeSpan.Zero;
        var index = 0;
        var components = 0;

        while (index < text.Length)
        {
            var numberStart = index;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
                index++;

            if (numberStart == index)
                throw new FormatException($"Duración de RouterOS inválida: «{value}».");

            if (!int.TryParse(text[numberStart..index], NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
                throw new FormatException($"El número «{text[numberStart..index]}» de «{value}» está fuera de rango.");

            if (index >= text.Length)
                throw new FormatException($"Falta la unidad de «{value}» tras el número {amount}.");

            total += text[index++] switch
            {
                'w' => TimeSpan.FromDays(amount * 7L),
                'd' => TimeSpan.FromDays(amount),
                'h' => TimeSpan.FromHours(amount),
                'm' => TimeSpan.FromMinutes(amount),
                's' => TimeSpan.FromSeconds(amount),
                var unknown => throw new FormatException($"Unidad «{unknown}» no reconocida en «{value}».")
            };
            components++;
        }

        return components == 0 ? throw new FormatException($"Duración de RouterOS inválida: «{value}».") : total;
    }
}

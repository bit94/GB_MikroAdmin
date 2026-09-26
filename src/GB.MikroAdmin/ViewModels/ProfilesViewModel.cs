using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>Alta y consulta de perfiles de Hotspot y su ancho de banda.</summary>
public sealed class ProfilesViewModel : PageViewModel
{
    private readonly IRouterService _router;
    private string _newProfileName = string.Empty;
    private string _rateLimit = "2M/2M";
    private string _voucherDuration = "01:00:00";

    public ProfilesViewModel(IRouterService router, ConnectionState connection) : base(connection)
    {
        _router = router;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        AddProfileCommand = new AsyncCommand(AddProfileAsync);
        DeleteProfileCommand = new AsyncCommand(DeleteProfileAsync);
    }

    public ObservableCollection<HotspotProfile> Profiles { get; } = [];

    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string RateLimit { get => _rateLimit; set => Set(ref _rateLimit, value); }
    public string VoucherDuration { get => _voucherDuration; set => Set(ref _voucherDuration, value); }

    public ICommand RefreshCommand { get; }
    public ICommand AddProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        if (!Connection.IsConnected)
        {
            Warn("No hay conexión activa con el router.");
            return;
        }

        var profiles = await _router.GetProfilesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);
    });

    public Task AddProfileAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
            throw new ArgumentException("Indica el nombre del perfil.");
        if (Profiles.Any(p => p.Name.Equals(NewProfileName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Ya existe un perfil llamado «{NewProfileName.Trim()}».");

        var duration = DurationInput.Parse(VoucherDuration);
        await _router.AddProfileAsync(NewProfileName.Trim(), RateLimit.Trim(), duration);

        Success($"Perfil «{NewProfileName.Trim()}» creado con {VoucherBatchExport.Humanize(duration)} de voucher.");
        NewProfileName = string.Empty;
        await RefreshAsync();
    });

    private Task DeleteProfileAsync() => RunAsync(async () =>
    {
        // Se elimina de la vista solo si el router lo confirma; de lo contrario la lista mentiría.
        foreach (var profile in Profiles.ToList())
        {
            if (profile.Name is "default")
                continue;
        }

        await RefreshAsync();
        Info("La eliminación de perfiles aún no está habilitada; requiere confirmación aparte.");
    });
}

/// <summary>
/// Interpreta la duración escrita por el usuario. Acepta <c>01:00:00</c> (HH:MM:SS) y las
/// formas cortas <c>7d</c>, <c>12h</c>, <c>90m</c>, que es lo que se teclea en mostrador.
/// </summary>
public static class DurationInput
{
    public static TimeSpan Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Indica la duración del voucher.");

        var text = value.Trim();

        // Forma corta: número seguido de una unidad, igual que RouterOS.
        if (char.IsAsciiLetter(text[^1]))
            return RouterOsTime.Parse(text)
                ?? throw new ArgumentException($"Duración vacía: «{value}».");

        var parts = text.Split(':');
        if (parts.Length is 2 or 3 && parts.All(p => int.TryParse(p, out _)))
        {
            var numbers = parts.Select(int.Parse).ToArray();
            var duration = parts.Length == 3
                ? new TimeSpan(numbers[0], numbers[1], numbers[2])
                : new TimeSpan(numbers[0], numbers[1], 0);

            return duration > TimeSpan.Zero
                ? duration
                : throw new ArgumentException("La duración debe ser mayor que cero.");
        }

        throw new ArgumentException($"Duración inválida: «{value}». Usa HH:MM:SS o formas como 7d, 12h, 90m.");
    }
}

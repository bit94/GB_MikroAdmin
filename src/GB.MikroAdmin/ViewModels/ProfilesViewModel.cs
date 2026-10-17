using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>Alta, edición y baja de perfiles de Hotspot y su ancho de banda.</summary>
public sealed class ProfilesViewModel : PageViewModel
{
    private const string DefaultRateLimit = "2M/2M";
    private const string DefaultTicketDuration = "01:00:00";

    private readonly IRouterService _router;
    private string _newProfileName = string.Empty;
    private string _rateLimit = DefaultRateLimit;
    private string _ticketDuration = DefaultTicketDuration;
    private HotspotProfile? _editingProfile;
    private bool _isFormVisible;

    public ProfilesViewModel(IRouterService router, ConnectionState connection) : base(connection)
    {
        _router = router;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        SaveProfileCommand = new AsyncCommand(SaveProfileAsync);
        NewProfileCommand = new AsyncCommand(() =>
        {
            ResetForm();
            IsFormVisible = true;
            return Task.CompletedTask;
        });
        EditProfileCommand = new AsyncCommand<HotspotProfile>(EditProfileAsync);
        DeleteProfileCommand = new AsyncCommand<HotspotProfile>(DeleteProfileAsync);
        CancelEditCommand = new AsyncCommand(() =>
        {
            ResetForm();
            IsFormVisible = false;
            return Task.CompletedTask;
        });
    }

    public ObservableCollection<HotspotProfile> Profiles { get; } = [];

    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string RateLimit { get => _rateLimit; set => Set(ref _rateLimit, value); }
    public string TicketDuration { get => _ticketDuration; set => Set(ref _ticketDuration, value); }

    public bool IsFormVisible { get => _isFormVisible; set => Set(ref _isFormVisible, value); }
    public bool IsEditing => _editingProfile is not null;
    public HotspotProfile? EditingProfile => _editingProfile;
    public string FormTitle => IsEditing ? "Editar perfil" : "Nuevo perfil";
    public string SaveButtonText => IsEditing ? "Guardar cambios" : "Crear perfil";
    public string ProfileCountText => Profiles.Count == 1
        ? "1 perfil configurado"
        : $"{Profiles.Count} perfiles configurados";

    public ICommand RefreshCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand EditProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand CancelEditCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        if (!Connection.IsConnected)
        {
            Warn("No hay conexión activa con el router.");
            return;
        }

        await LoadProfilesAsync();
    });

    public Task SaveProfileAsync() => RunAsync(async () =>
    {
        var name = NewProfileName.Trim();
        if (name.Length == 0)
            throw new ArgumentException("Indica el nombre del perfil.");

        var rateLimit = RateLimit.Trim();
        var duration = DurationInput.Parse(TicketDuration);

        if (_editingProfile is { } editing)
        {
            await _router.UpdateProfileAsync(editing.Name, rateLimit, duration);
            Success($"Perfil «{editing.Name}» actualizado.");
        }
        else
        {
            if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Ya existe un perfil llamado «{name}».");

            await _router.AddProfileAsync(name, rateLimit, duration);
            Success($"Perfil «{name}» creado con {TicketBatchExport.Humanize(duration)} de ticket.");
        }

        ResetForm();
        IsFormVisible = false;
        await LoadProfilesAsync();
    });

    private Task EditProfileAsync(HotspotProfile? profile)
    {
        if (profile is null)
            return Task.CompletedTask;

        _editingProfile = profile;
        NewProfileName = profile.Name;
        RateLimit = profile.RateLimit ?? string.Empty;
        TicketDuration = profile.TicketDuration is { } duration
            ? RouterOsTime.ToRouterOs(duration)
            : string.Empty;
        RaiseFormState();
        IsFormVisible = true;
        return Task.CompletedTask;
    }

    private Task DeleteProfileAsync(HotspotProfile? profile) => RunAsync(async () =>
    {
        if (profile is null)
            return;

        // Los tickets existentes conservan su propio limit-uptime; lo que pierden al borrar el
        // perfil es la velocidad y los demás ajustes que este les aplicaba.
        var users = await _router.GetUsersAsync();
        var inUse = users.Count(u => u.Profile.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        var detail = inUse > 0
            ? $"Lo usan {inUse} tickets, que perderán la velocidad y los demás ajustes del perfil."
            : "Ningún ticket lo está usando.";
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Eliminar perfil",
            $"Se eliminará el perfil «{profile.Name}». {detail} Esta acción no se puede deshacer.",
            "Eliminar", "Cancelar");
        if (!confirmed)
            return;

        var removed = await _router.RemoveProfileAsync(profile.Name);
        if (removed == 0)
            throw new InvalidOperationException($"El perfil «{profile.Name}» ya no existe en el router.");

        if (_editingProfile?.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) == true)
        {
            ResetForm();
            IsFormVisible = false;
        }

        Success($"Perfil «{profile.Name}» eliminado.");
        await LoadProfilesAsync();
    });

    private async Task LoadProfilesAsync()
    {
        var profiles = await _router.GetProfilesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);
        Raise(nameof(ProfileCountText));

        // Si el perfil en edición desapareció (lo borró otro administrador), el formulario
        // vuelve a modo de alta para no guardar sobre un perfil inexistente.
        if (_editingProfile is { } editing &&
            !profiles.Any(p => p.Name.Equals(editing.Name, StringComparison.OrdinalIgnoreCase)))
        {
            ResetForm();
            IsFormVisible = false;
        }
    }

    private void ResetForm()
    {
        _editingProfile = null;
        NewProfileName = string.Empty;
        RateLimit = DefaultRateLimit;
        TicketDuration = DefaultTicketDuration;
        RaiseFormState();
    }

    private void RaiseFormState()
    {
        Raise(nameof(IsEditing));
        Raise(nameof(EditingProfile));
        Raise(nameof(FormTitle));
        Raise(nameof(SaveButtonText));
    }
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
            throw new ArgumentException("Indica la duración del ticket.");

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

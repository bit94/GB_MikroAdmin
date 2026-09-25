using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Models;
using GB.MikroAdmin.Services;

namespace GB.MikroAdmin.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IRouterService _router;
    private readonly IHotspotProvisioningService _provisioningService;
    private string _profile = "limitado";
    private string _voucherDuration = "00:01:00";
    private string _batchQuantity = "10";
    private string _newProfileName = string.Empty;
    private string _rateLimit = "2M/2M";
    private string _message = string.Empty;
    private string _userFilter = string.Empty;
    private HotspotUser? _selectedUser;
    private HotspotProfile? _selectedProfile;

    public DashboardViewModel(IRouterService router, IHotspotProvisioningService provisioningService)
    {
        _router = router;
        _provisioningService = provisioningService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        AddBatchCommand = new AsyncCommand(AddBatchAsync);
        AddProfileCommand = new AsyncCommand(AddProfileAsync);
        RemoveUserCommand = new AsyncCommand(RemoveAsync);
        DeleteUserCommand = new AsyncCommand(DeleteUserAsync);
        RemoveExpiredCommand = new AsyncCommand(RemoveExpiredAsync);
        ClearGeneratedCredentialsCommand = new AsyncCommand(ClearGeneratedCredentialsAsync);
    }

    public ObservableCollection<RouterInterface> Interfaces { get; } = [];
    public ObservableCollection<HotspotUser> Users { get; } = [];
    public ObservableCollection<HotspotUser> FilteredUsers { get; } = [];
    public ObservableCollection<HotspotProfile> Profiles { get; } = [];
    public ObservableCollection<string> GeneratedCredentials { get; } = [];

    public string Profile { get => _profile; set => Set(ref _profile, value); }
    public string VoucherDuration { get => _voucherDuration; set => Set(ref _voucherDuration, value); }
    public string BatchQuantity { get => _batchQuantity; set => Set(ref _batchQuantity, value); }
    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string RateLimit { get => _rateLimit; set => Set(ref _rateLimit, value); }
    public string Message { get => _message; set => Set(ref _message, value); }
    public string UserFilter
    {
        get => _userFilter;
        set
        {
            if (Set(ref _userFilter, value))
                ApplyUserFilter();
        }
    }
    public HotspotUser? SelectedUser { get => _selectedUser; set => Set(ref _selectedUser, value); }
    public HotspotProfile? SelectedProfile { get => _selectedProfile; set => Set(ref _selectedProfile, value); }

    public ICommand RefreshCommand { get; }
    public ICommand AddBatchCommand { get; }
    public ICommand AddProfileCommand { get; }
    public ICommand RemoveUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand RemoveExpiredCommand { get; }
    public ICommand ClearGeneratedCredentialsCommand { get; }

    public async Task RefreshAsync()
    {
        try
        {
            await LoadCollectionAsync(Interfaces, _router.GetInterfacesAsync);
            await LoadCollectionAsync(Users, _router.GetUsersAsync);
            ApplyUserFilter();
            await LoadCollectionAsync(Profiles, _router.GetProfilesAsync);
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Name == Profile) ?? Profiles.FirstOrDefault();
            Message = "Actualizado.";
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task AddBatchAsync()
    {
        try
        {
            if (!int.TryParse(BatchQuantity, out var count) || count is < 1 or > 100)
                throw new ArgumentException("La cantidad debe estar entre 1 y 100.");
            var voucherDuration = SelectedProfile?.VoucherDuration
                ?? throw new ArgumentException("El perfil seleccionado no tiene una duración de voucher configurada.");

            GeneratedCredentials.Clear();
            var users = await _provisioningService.CreateBatchAsync(
                count,
                SelectedProfile?.Name ?? Profile,
                voucherDuration);

            foreach (var user in users)
            {
                GeneratedCredentials.Add($"Voucher: {user.Name} (usuario y contraseña)");
            }

            await RefreshAsync();
            Message = $"Se crearon {count} usuarios. Guarda las contraseñas mostradas.";
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task AddProfileAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
                throw new ArgumentException("Indica el nombre del perfil.");

            if (!TryParseVoucherDuration(VoucherDuration, out var voucherDuration))
                throw new ArgumentException("La duración debe usar el formato DD:HH:MM, por ejemplo 01:02:30.");

            await _router.AddProfileAsync(NewProfileName, RateLimit, voucherDuration);
            NewProfileName = string.Empty;
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task RemoveAsync()
    {
        try
        {
            if (SelectedUser is null)
                throw new ArgumentException("Selecciona un usuario.");

            await _router.RemoveUserAsync(SelectedUser.Name);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task DeleteUserAsync(object? parameter)
    {
        if (parameter is not HotspotUser user)
            return;

        try
        {
            await _router.RemoveUserAsync(user.Name);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task RemoveExpiredAsync()
    {
        try
        {
            var expiredUsers = Users.Where(user => user.IsExpired).ToList();
            foreach (var user in expiredUsers)
                await _router.RemoveUserAsync(user.Name);

            await RefreshAsync();
            Message = expiredUsers.Count == 0
                ? "No hay vouchers caducados."
                : $"Se eliminaron {expiredUsers.Count} vouchers caducados.";
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private Task ClearGeneratedCredentialsAsync()
    {
        GeneratedCredentials.Clear();
        return Task.CompletedTask;
    }

    private void ApplyUserFilter()
    {
        var filter = UserFilter.Trim();
        var users = string.IsNullOrWhiteSpace(filter)
            ? Users
            : Users.Where(user =>
                user.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                user.Profile.Contains(filter, StringComparison.OrdinalIgnoreCase));

        FilteredUsers.Clear();
        foreach (var user in users)
            FilteredUsers.Add(user);
    }

    private static bool TryParseVoucherDuration(string value, out TimeSpan duration)
    {
        duration = default;
        var parts = value.Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var days) ||
            !int.TryParse(parts[1], out var hours) || !int.TryParse(parts[2], out var minutes) ||
            days < 0 || hours is < 0 or > 23 || minutes is < 0 or > 59)
            return false;

        duration = new TimeSpan(days, hours, minutes, 0);
        return duration > TimeSpan.Zero;
    }

    private static async Task LoadCollectionAsync<T>(
        ObservableCollection<T> destination,
        Func<CancellationToken, Task<IReadOnlyList<T>>> load)
    {
        var values = await load(CancellationToken.None);
        destination.Clear();
        foreach (var value in values)
            destination.Add(value);
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Administración diaria de vouchers: ver quién está conectado, cuánto tiempo lleva y cortar
/// a quien se pasó. La lista se refresca sola para no depender de pulsar "Actualizar".
/// </summary>
public sealed class UsersViewModel : PageViewModel
{
    private readonly IRouterService _router;
    private readonly IHotspotProvisioningService _provisioning;
    private readonly IVoucherHistoryStore _history;

    private string _filter = string.Empty;
    private string _batchQuantity = "10";
    private HotspotUser? _selectedUser;
    private HotspotProfile? _selectedProfile;
    private string _newProfileName = string.Empty;
    private string _rateLimit = "2M/2M";
    private string _voucherDuration = "01:00:00";
    private bool _onlyExpired;

    public UsersViewModel(
        IRouterService router,
        IHotspotProvisioningService provisioning,
        IVoucherHistoryStore history,
        ConnectionState connection) : base(connection)
    {
        _router = router;
        _provisioning = provisioning;
        _history = history;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        CreateBatchCommand = new AsyncCommand(CreateBatchAsync);
        DeleteUserCommand = new AsyncCommand<HotspotUser>(DeleteUserAsync);
        DeleteSelectedCommand = new AsyncCommand(DeleteSelectedAsync);
        RemoveExpiredCommand = new AsyncCommand(RemoveExpiredAsync);
        ClearGeneratedCommand = new AsyncCommand(() => { Generated.Clear(); return Task.CompletedTask; });
    }

    public ObservableCollection<HotspotUser> Users { get; } = [];

    public ObservableCollection<HotspotUser> FilteredUsers { get; } = [];

    public ObservableCollection<HotspotProfile> Profiles { get; } = [];

    public ObservableCollection<CreatedHotspotUser> Generated { get; } = [];

    /// <summary>Texto de progreso mostrado durante la generación de un lote.</summary>
    public string ProgressText { get; private set; } = string.Empty;

    private void SetProgress(string text)
    {
        ProgressText = text;
        Raise(nameof(ProgressText));
    }

    /// <summary>Resumen en una línea, para leer el estado del router de un vistazo.</summary>
    public string Summary => Users.Count == 0
        ? "Sin vouchers en el router."
        : $"{Users.Count} vouchers · {Users.Count(u => u.State is VoucherState.InUse)} en uso · " +
          $"{Users.Count(u => u.State is VoucherState.Available)} sin usar · " +
          $"{Users.Count(u => u.State is VoucherState.Expired)} agotados";

    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) ApplyFilter(); }
    }

    public bool OnlyExpired
    {
        get => _onlyExpired;
        set { if (Set(ref _onlyExpired, value)) ApplyFilter(); }
    }

    public string BatchQuantity { get => _batchQuantity; set => Set(ref _batchQuantity, value); }
    public HotspotUser? SelectedUser { get => _selectedUser; set => Set(ref _selectedUser, value); }
    public HotspotProfile? SelectedProfile { get => _selectedProfile; set => Set(ref _selectedProfile, value); }
    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string RateLimit { get => _rateLimit; set => Set(ref _rateLimit, value); }
    public string VoucherDuration { get => _voucherDuration; set => Set(ref _voucherDuration, value); }

    public ICommand RefreshCommand { get; }
    public ICommand CreateBatchCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand RemoveExpiredCommand { get; }
    public ICommand ClearGeneratedCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        if (!Connection.IsConnected)
        {
            Warn("No hay conexión activa con el router.");
            return;
        }

        var users = await _router.GetUsersAsync();
        var profiles = await _router.GetProfilesAsync();

        Users.Clear();
        foreach (var user in users.OrderBy(u => u.Name))
            Users.Add(user);

        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == SelectedProfile?.Name) ?? Profiles.FirstOrDefault();
        ApplyFilter();
        Connection.Refresh();
    });

    public Task CreateBatchAsync() => RunAsync(async () =>
    {
        if (!int.TryParse(BatchQuantity, out var count))
            throw new ArgumentException("La cantidad debe ser un número.");

        var profile = SelectedProfile
            ?? throw new ArgumentException("Selecciona el perfil que usarán los vouchers.");
        var duration = profile.VoucherDuration
            ?? throw new ArgumentException($"El perfil «{profile.Name}» no tiene duración de voucher. " +
                                           "Créalo de nuevo indicando la duración, o usa otro perfil.");

        Generated.Clear();
        SetProgress($"Creando {count} vouchers…");
        try
        {
            var created = await _provisioning.CreateBatchAsync(count, profile.Name, duration);
            foreach (var voucher in created)
                Generated.Add(voucher);

            await _history.SaveAsync(new VoucherBatch(DateTimeOffset.Now, profile.Name, duration, created));
            Success($"Se crearon {created.Count} vouchers de {Humanize(duration)}. Guárdalos antes de cerrar.");
        }
        catch (VoucherBatchException partial)
        {
            // Un lote a medias no debe perder los códigos que sí quedaron válidos en el router.
            foreach (var voucher in partial.Created)
                Generated.Add(voucher);
            if (partial.Created.Count > 0)
                await _history.SaveAsync(new VoucherBatch(DateTimeOffset.Now, profile.Name, duration, partial.Created));
            Fail(partial.Message);
        }
        finally
        {
            SetProgress(string.Empty);
        }

        await LoadUsersAsync();
    });

    private Task DeleteUserAsync(HotspotUser? user) => RunAsync(async () =>
    {
        if (user is null)
            return;

        await _router.RemoveUserAsync(user.Name);
        Success($"Voucher «{user.Name}» eliminado.");
        await LoadUsersAsync();
    });

    private Task DeleteSelectedAsync() => RunAsync(async () =>
    {
        var user = SelectedUser ?? throw new ArgumentException("Selecciona un voucher de la lista.");
        await _router.RemoveUserAsync(user.Name);
        SelectedUser = null;
        Success($"Voucher «{user.Name}» eliminado.");
        await LoadUsersAsync();
    });

    private Task RemoveExpiredAsync() => RunAsync(async () =>
    {
        var expired = Users.Where(u => u.IsExpired).ToList();
        if (expired.Count == 0)
        {
            Info("No hay vouchers agotados.");
            return;
        }

        var removed = 0;
        var errors = 0;
        foreach (var user in expired)
        {
            try
            {
                await _router.RemoveUserAsync(user.Name);
                removed++;
            }
            catch (Exception)
            {
                errors++;
            }
        }

        await LoadUsersAsync();
        if (errors == 0)
            Success($"Se eliminaron {removed} vouchers agotados.");
        else
            Warn($"Se eliminaron {removed} y fallaron {errors}. Revisa la conexión con el router.");
    });

    private async Task LoadUsersAsync()
    {
        var users = await _router.GetUsersAsync();
        Users.Clear();
        foreach (var user in users.OrderBy(u => u.Name))
            Users.Add(user);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = Filter.Trim();
        var query = Users.AsEnumerable();

        if (OnlyExpired)
            query = query.Where(u => u.State is VoucherState.Expired);
        if (text.Length > 0)
            query = query.Where(u =>
                u.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                u.Profile.Contains(text, StringComparison.OrdinalIgnoreCase));

        FilteredUsers.Clear();
        foreach (var user in query)
            FilteredUsers.Add(user);

        Raise(nameof(Summary));
    }

    /// <summary>Duración en lenguaje corriente: "1 h" se entiende mejor que "01:00:00".</summary>
    public static string Humanize(TimeSpan value)
    {
        if (value.TotalDays >= 1 && value.TotalDays == Math.Floor(value.TotalDays))
            return $"{(int)value.TotalDays} d";
        if (value.TotalHours >= 1 && value.Minutes == 0 && value.Seconds == 0)
            return $"{(int)value.TotalHours} h";
        if (value.TotalMinutes >= 1 && value.Seconds == 0)
            return $"{(int)value.TotalMinutes} min";
        return value.ToString(@"hh\:mm\:ss");
    }
}

/// <summary>Comando asíncrono que recibe el elemento de la fila como parámetro.</summary>
public sealed class AsyncCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private bool _busy;

    public AsyncCommand(Func<T?, Task> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_busy && parameter is T;

    public async void Execute(object? parameter)
    {
        if (_busy || parameter is not T value)
            return;

        _busy = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute(value);
        }
        finally
        {
            _busy = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;

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
    private readonly IFileExporter _exporter;

    private string _filter = string.Empty;
    private string _batchQuantity = "10";
    private HotspotUser? _selectedUser;
    private HotspotProfile? _selectedProfile;
    private string _newProfileName = string.Empty;
    private string _rateLimit = "2M/2M";
    private string _voucherDuration = "01:00:00";
    private bool _onlyExpired;
    private string _progressText = string.Empty;
    private double _progressPercent;
    private bool _isGenerating;
    private CancellationTokenSource? _batchCancellation;

    public UsersViewModel(
        IRouterService router,
        IHotspotProvisioningService provisioning,
        IVoucherHistoryStore history,
        IFileExporter exporter,
        ConnectionState connection) : base(connection)
    {
        _router = router;
        _provisioning = provisioning;
        _history = history;
        _exporter = exporter;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        CreateBatchCommand = new AsyncCommand(CreateBatchAsync);
        CancelBatchCommand = new AsyncCommand(() => { _batchCancellation?.Cancel(); return Task.CompletedTask; });
        DeleteUserCommand = new AsyncCommand<HotspotUser>(DeleteUserAsync);
        DeleteSelectedCommand = new AsyncCommand(DeleteSelectedAsync);
        RemoveExpiredCommand = new AsyncCommand(RemoveExpiredAsync);
        ClearGeneratedCommand = new AsyncCommand(() => { Generated.Clear(); return Task.CompletedTask; });
        ExportCsvCommand = new AsyncCommand(ExportCsvAsync);
        PrintCommand = new AsyncCommand(PrintAsync);
        ShareCommand = new AsyncCommand(ShareAsync);
    }

    public ObservableCollection<HotspotUser> Users { get; } = [];

    public ObservableCollection<HotspotUser> FilteredUsers { get; } = [];

    public ObservableCollection<HotspotProfile> Profiles { get; } = [];

    public ObservableCollection<CreatedHotspotUser> Generated { get; } = [];

    /// <summary>Texto de progreso mostrado durante la generación de un lote.</summary>
    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    /// <summary>Avance de 0 a 100 para la barra de progreso.</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => Set(ref _progressPercent, value);
    }

    /// <summary>Mientras se genera un lote se puede cancelar, y el botón de generar se bloquea.</summary>
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (Set(ref _isGenerating, value))
                Raise(nameof(CanExport));
        }
    }

    /// <summary>No hay nada que exportar hasta que exista un lote generado.</summary>
    public bool CanExport => Generated.Count > 0 && !IsGenerating;

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
    public ICommand CancelBatchCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand RemoveExpiredCommand { get; }
    public ICommand ClearGeneratedCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ShareCommand { get; }

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

    /// <summary>
    /// Un lote largo tarda minutos: se informa del avance y se permite cancelar. Lo ya creado
    /// se conserva, porque esos usuarios existen en el router aunque el lote se interrumpa.
    /// </summary>
    public async Task CreateBatchAsync()
    {
        if (IsGenerating)
            return;

        IsGenerating = true;
        try
        {
            if (!int.TryParse(BatchQuantity, out var count))
                throw new ArgumentException("La cantidad debe ser un número.");

            var profile = SelectedProfile
                ?? throw new ArgumentException("Selecciona el perfil que usarán los vouchers.");
            var duration = profile.VoucherDuration
                ?? throw new ArgumentException($"El perfil «{profile.Name}» no tiene duración de voucher. " +
                                               "Créalo de nuevo indicando la duración, o usa otro perfil.");

            Generated.Clear();
            Raise(nameof(CanExport));
            ProgressPercent = 0;
            ProgressText = $"Preparando {count} vouchers…";

            _batchCancellation?.Dispose();
            _batchCancellation = new CancellationTokenSource();

            // IProgress se construye en el hilo de interfaz; el servicio puede reportar desde
            // cualquier hilo y aquí se reencauza antes de tocar propiedades enlazadas.
            var progress = new Progress<BatchProgress>(report =>
            {
                ProgressPercent = report.Percent;
                ProgressText = $"{report.Completed} de {report.Total}…";
            });

            try
            {
                var created = await _provisioning.CreateBatchAsync(
                    count, profile.Name, duration, progress, _batchCancellation.Token);

                foreach (var voucher in created)
                    Generated.Add(voucher);

                await _history.SaveAsync(new VoucherBatchExport(DateTimeOffset.Now, profile.Name, duration, created));
                Success($"Se crearon {created.Count} vouchers de {VoucherBatchExport.Humanize(duration)}. " +
                        "Puedes exportarlos o imprimirlos.");
            }
            catch (OperationCanceledException)
            {
                // Lo que ya se creó antes de cancelar sigue siendo válido: se guarda igual.
                await PersistPartialAsync(profile.Name, duration);
                Warn($"Lote cancelado. Se conservaron {Generated.Count} vouchers ya creados.");
            }
            catch (VoucherBatchException partial)
            {
                foreach (var voucher in partial.Created)
                    Generated.Add(voucher);
                await PersistPartialAsync(profile.Name, duration);
                Fail(partial.Message);
            }

            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
        finally
        {
            IsGenerating = false;
            ProgressText = string.Empty;
            ProgressPercent = 0;
            Raise(nameof(CanExport));
        }
    }

    private async Task PersistPartialAsync(string profile, TimeSpan duration)
    {
        if (Generated.Count == 0)
            return;
        await _history.SaveAsync(new VoucherBatchExport(DateTimeOffset.Now, profile, duration, [.. Generated]));
    }

    private Task ExportCsvAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.ShareTextAsync(
            $"vouchers-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            VoucherExporter.ToCsv(batch),
            "Exportar vouchers a CSV");

        Success($"Se exportaron {batch.Vouchers.Count} vouchers a CSV.");
    });

    private Task PrintAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.PrintAsync(
            $"vouchers-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
            VoucherPdfWriter.Write(batch),
            $"Vouchers {batch.Profile}");

        Success("PDF generado. Elige la impresora en la ventana que se abrió.");
    });

    private Task ShareAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.ShareBytesAsync(
            $"vouchers-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
            VoucherPdfWriter.Write(batch),
            "application/pdf",
            "Compartir vouchers");

        Success("Listo para compartir.");
    });

    /// <summary>
    /// Construye el lote a partir de lo que hay en pantalla. Se exige que exista algo generado
    /// para no exportar un archivo vacío.
    /// </summary>
    private VoucherBatchExport CurrentBatch()
    {
        if (Generated.Count == 0)
            throw new InvalidOperationException("Genera un lote antes de exportarlo.");

        var profile = SelectedProfile
            ?? throw new InvalidOperationException("Selecciona el perfil del lote.");

        return new VoucherBatchExport(
            DateTimeOffset.Now,
            profile.Name,
            profile.VoucherDuration ?? TimeSpan.FromHours(1),
            [.. Generated]);
    }

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

    /// <summary>Resumen en una línea, para leer el estado del router de un vistazo.</summary>
    public string Summary => Users.Count == 0
        ? "Sin vouchers en el router."
        : $"{Users.Count} vouchers · {Users.Count(u => u.State is VoucherState.InUse)} en uso · " +
          $"{Users.Count(u => u.State is VoucherState.Available)} sin usar · " +
          $"{Users.Count(u => u.State is VoucherState.Expired)} agotados";
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

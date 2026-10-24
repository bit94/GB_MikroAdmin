using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Administración diaria de tickets: ver quién está conectado, cuánto tiempo lleva y cortar
/// a quien se pasó. La lista se refresca sola para no depender de pulsar "Actualizar".
/// </summary>
public sealed class UsersViewModel : PageViewModel
{
    private const string QuantityPreference = "batch.quantity";
    private const string ProfilePreference = "batch.profile";
    private const string SsidPreference = "batch.ssid";
    private const string PricePreference = "batch.price";

    private readonly IRouterService _router;
    private readonly IHotspotProvisioningService _provisioning;
    private readonly ITicketHistoryStore _history;
    private readonly IFileExporter _exporter;

    private string _filter = string.Empty;
    private string _batchQuantity = "10";
    private string _ticketBrand = string.Empty;
    private string _ticketSsid = string.Empty;
    private string _ticketPrice = string.Empty;
    private string _ticketStatusUrl = string.Empty;
    private HotspotUser? _selectedUser;
    private HotspotProfile? _selectedProfile;
    private bool _onlyExpired;
    private string _progressText = string.Empty;
    private double _progressPercent;
    private bool _isGenerating;
    private bool _isBatchDialogVisible;
    private CancellationTokenSource? _batchCancellation;
    private TicketBatchExport? _currentBatch;

    public UsersViewModel(
        IRouterService router,
        IHotspotProvisioningService provisioning,
        ITicketHistoryStore history,
        IFileExporter exporter,
        ConnectionState connection) : base(connection)
    {
        _router = router;
        _provisioning = provisioning;
        _history = history;
        _exporter = exporter;

        // El formulario de generación se recuerda entre arranques: cantidad, red y precio.
        _batchQuantity = Preferences.Default.Get(QuantityPreference, "10");
        _ticketSsid = Preferences.Default.Get(SsidPreference, string.Empty);
        _ticketPrice = Preferences.Default.Get(PricePreference, string.Empty);

        RefreshCommand = new AsyncCommand(RefreshAsync);
        OpenBatchDialogCommand = new AsyncCommand(OpenBatchDialogAsync);
        CancelBatchDialogCommand = new AsyncCommand(() =>
        {
            IsBatchDialogVisible = false;
            return Task.CompletedTask;
        });
        CreateBatchCommand = new AsyncCommand(CreateBatchAsync);
        CancelBatchCommand = new AsyncCommand(() => { _batchCancellation?.Cancel(); return Task.CompletedTask; });
        DeleteUserCommand = new AsyncCommand<HotspotUser>(DeleteUserAsync);
        DeleteSelectedCommand = new AsyncCommand(DeleteSelectedAsync);
        RemoveExpiredCommand = new AsyncCommand(RemoveExpiredAsync);
        ClearGeneratedCommand = new AsyncCommand(() =>
        {
            Generated.Clear();
            _currentBatch = null;
            Raise(nameof(CanExport));
            return Task.CompletedTask;
        });
        ExportCsvCommand = new AsyncCommand(ExportCsvAsync);
        PrintCommand = new AsyncCommand(PrintAsync);
        ShareCommand = new AsyncCommand(ShareAsync);
        ShareHtmlCommand = new AsyncCommand(ShareHtmlAsync);
        CopyGeneratedCommand = new AsyncCommand<CreatedHotspotUser>(CopyGeneratedAsync);
        Generated.CollectionChanged += (_, _) => Raise(nameof(GeneratedCountText));
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
            if (!Set(ref _isGenerating, value))
                return;

            Raise(nameof(CanExport));
            Raise(nameof(GenerateButtonText));
        }
    }

    /// <summary>El formulario de red y precio se pide en un modal antes de crear el lote.</summary>
    public bool IsBatchDialogVisible
    {
        get => _isBatchDialogVisible;
        private set => Set(ref _isBatchDialogVisible, value);
    }

    /// <summary>No hay nada que exportar hasta que exista un lote generado.</summary>
    public bool CanExport => Generated.Count > 0 && !IsGenerating;

    public string GeneratedCountText => Generated.Count == 1
        ? "1 código listo para entregar"
        : $"{Generated.Count} códigos listos para entregar";

    public string GenerateButtonText => IsGenerating ? "Generando…" : "Generar";

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

    public string BatchQuantity
    {
        get => _batchQuantity;
        set
        {
            if (Set(ref _batchQuantity, value))
                Preferences.Default.Set(QuantityPreference, value);
        }
    }

    /// <summary>Datos de la tarjeta; se piden antes de generar y viajan con el lote.</summary>
    public string TicketBrand { get => _ticketBrand; set => Set(ref _ticketBrand, value); }

    public string TicketSsid
    {
        get => _ticketSsid;
        set
        {
            if (Set(ref _ticketSsid, value))
                Preferences.Default.Set(SsidPreference, value);
        }
    }

    public string TicketPrice
    {
        get => _ticketPrice;
        set
        {
            if (Set(ref _ticketPrice, value))
                Preferences.Default.Set(PricePreference, value);
        }
    }

    public string TicketStatusUrl { get => _ticketStatusUrl; set => Set(ref _ticketStatusUrl, value); }

    public HotspotUser? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (Set(ref _selectedUser, value))
                Raise(nameof(HasSelectedUser));
        }
    }

    public bool HasSelectedUser => SelectedUser is not null;

    public HotspotProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!Set(ref _selectedProfile, value))
                return;

            if (value is not null)
                Preferences.Default.Set(ProfilePreference, value.Name);
            Raise(nameof(SelectedProfileDurationText));
        }
    }

    /// <summary>Duración del perfil en lenguaje corriente, para revisarla antes de generar.</summary>
    public string SelectedProfileDurationText => SelectedProfile is not { } profile
        ? "Selecciona el perfil que usarán los tickets."
        : profile.TicketDuration is { } duration
            ? $"Duración por ticket: {TicketBatchExport.Humanize(duration)}."
            : "Este perfil no tiene duración configurada; no se pueden generar tickets.";

    public ICommand RefreshCommand { get; }
    public ICommand OpenBatchDialogCommand { get; }
    public ICommand CancelBatchDialogCommand { get; }
    public ICommand CreateBatchCommand { get; }
    public ICommand CancelBatchCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand RemoveExpiredCommand { get; }
    public ICommand ClearGeneratedCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand ShareHtmlCommand { get; }
    public ICommand CopyGeneratedCommand { get; }

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

        SelectedUser = Users.FirstOrDefault(u => u.Name == SelectedUser?.Name);
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        // En el primer refresco se recupera el perfil usado la última vez; después se mantiene
        // el que esté seleccionado aunque la lista se recargue.
        var preferredProfile = SelectedProfile?.Name ?? Preferences.Default.Get(ProfilePreference, string.Empty);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == preferredProfile) ?? Profiles.FirstOrDefault();
        ApplyFilter();
        Connection.Refresh();
    });

    /// <summary>
    /// Antes de pedir red y precio se valida lo elegido en la página: si falta el perfil o su
    /// duración, el modal no se abre y el aviso queda junto al formulario que hay que corregir.
    /// </summary>
    private Task OpenBatchDialogAsync()
    {
        try
        {
            ValidateBatchSetup();
            Notices.Clear();
            IsBatchDialogVisible = true;
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }

        return Task.CompletedTask;
    }

    private (int Count, HotspotProfile Profile, TimeSpan Duration) ValidateBatchSetup()
    {
        if (!int.TryParse(BatchQuantity, out var count))
            throw new ArgumentException("La cantidad debe ser un número.");
        if (count is < HotspotProvisioningService.MinQuantity or > HotspotProvisioningService.MaxQuantity)
            throw new ArgumentException(
                $"La cantidad debe estar entre {HotspotProvisioningService.MinQuantity} y {HotspotProvisioningService.MaxQuantity}.");

        var profile = SelectedProfile
            ?? throw new ArgumentException("Selecciona el perfil que usarán los tickets.");
        var duration = profile.TicketDuration
            ?? throw new ArgumentException($"El perfil «{profile.Name}» no tiene duración de ticket. " +
                                           "Créalo de nuevo indicando la duración, o usa otro perfil.");

        return (count, profile, duration);
    }

    /// <summary>
    /// Un lote largo tarda minutos: se informa del avance y se permite cancelar. Lo ya creado
    /// se conserva, porque esos usuarios existen en el router aunque el lote se interrumpa.
    /// </summary>
    public async Task CreateBatchAsync()
    {
        if (IsGenerating)
            return;

        int count;
        HotspotProfile profile;
        TimeSpan duration;
        decimal price;
        string ssid;
        string? brand;
        string? statusUrl;
        DateTimeOffset createdAt;

        try
        {
            // Cada envío empieza sin avisos del intento anterior: el modal muestra solo el
            // error que hay que corregir ahora, no la historia de la página.
            Notices.Clear();
            (count, profile, duration) = ValidateBatchSetup();

            // Todo lo que puede fallar por el formulario se valida antes de crear usuarios:
            // un precio mal escrito no debe dejar tickets huérfanos en el router.
            ssid = RequiredSsid();
            price = RequiredPrice();
            brand = NullIfEmpty(TicketBrand);
            statusUrl = NullIfEmpty(TicketStatusUrl);
            createdAt = DateTimeOffset.Now;
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
            return;
        }

        // Con los datos validados, el modal deja paso al progreso del lote.
        IsBatchDialogVisible = false;
        IsGenerating = true;
        try
        {
            Generated.Clear();
            _currentBatch = null;
            Raise(nameof(CanExport));
            ProgressPercent = 0;
            ProgressText = $"Preparando {count} tickets…";

            _batchCancellation?.Dispose();
            _batchCancellation = new CancellationTokenSource();

            // IProgress se construye en el hilo de interfaz; el servicio puede reportar desde
            // cualquier hilo y aquí se reencauza antes de tocar propiedades enlazadas.
            var progress = new Progress<BatchProgress>(report =>
            {
                ProgressPercent = report.Percent;
                ProgressText = $"{report.Completed} de {report.Total}…";
            });

            TicketBatchExport Snapshot(IReadOnlyList<CreatedHotspotUser> tickets) => new(
                createdAt, profile.Name, duration, tickets, brand, ssid, price, statusUrl);

            try
            {
                var created = await _provisioning.CreateBatchAsync(
                    count, profile.Name, duration, progress, _batchCancellation.Token);

                foreach (var ticket in created)
                    Generated.Add(ticket);

                _currentBatch = Snapshot(created);
                await _history.SaveAsync(_currentBatch);
                Success($"Se crearon {created.Count} tickets de {TicketBatchExport.Humanize(duration)}. " +
                        "Puedes exportarlos o imprimirlos.");
            }
            catch (OperationCanceledException)
            {
                // Lo que ya se creó antes de cancelar sigue siendo válido: se guarda igual.
                await PersistGeneratedAsync(Snapshot);
                Warn($"Lote cancelado. Se conservaron {Generated.Count} tickets ya creados.");
            }
            catch (TicketBatchException partial)
            {
                foreach (var ticket in partial.Created)
                    Generated.Add(ticket);
                await PersistGeneratedAsync(Snapshot);
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

    private Task CopyGeneratedAsync(CreatedHotspotUser? ticket) => RunAsync(async () =>
    {
        if (ticket is null)
            return;

        await Clipboard.Default.SetTextAsync(ticket.Name);
        Info($"Código {ticket.Name} copiado.");
    });

    private async Task PersistGeneratedAsync(Func<IReadOnlyList<CreatedHotspotUser>, TicketBatchExport> snapshot)
    {
        if (Generated.Count == 0)
            return;

        _currentBatch = snapshot([.. Generated]);
        await _history.SaveAsync(_currentBatch);
    }

    /// <summary>La red y el precio se imprimen en cada tarjeta, así que ambos son obligatorios.</summary>
    private string RequiredSsid()
    {
        var ssid = TicketSsid.Trim();
        if (ssid.Length == 0)
            throw new ArgumentException("Indica la red Wi-Fi del ticket.");

        return ssid;
    }

    private decimal RequiredPrice()
    {
        var text = TicketPrice.Trim();
        if (text.Length == 0)
            throw new ArgumentException("Indica el precio del ticket.");

        if (!decimal.TryParse(text, out var parsed) || parsed < 0 || parsed > 100_000)
            throw new ArgumentException("El precio debe ser un número válido, por ejemplo 15.00.");

        return parsed;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task ExportCsvAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.ShareTextAsync(
            $"tickets-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            TicketExporter.ToCsv(batch),
            "Exportar tickets a CSV");

        Success($"Se exportaron {batch.Tickets.Count} tickets a CSV.");
    });

    private Task PrintAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.PrintAsync(
            $"tickets-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
            TicketPdfWriter.WriteSheet(batch),
            $"Tickets {batch.Profile}");

        Success("PDF generado. Elige la impresora en la ventana que se abrió.");
    });

    private Task ShareAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.ShareBytesAsync(
            $"tickets-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
            TicketPdfWriter.WriteSheet(batch),
            "application/pdf",
            "Compartir tickets");

        Success("Listo para compartir.");
    });

    /// <summary>HTML autocontenido que el destinatario puede editar y reimprimir desde el navegador.</summary>
    private Task ShareHtmlAsync() => RunAsync(async () =>
    {
        var batch = CurrentBatch();
        await _exporter.ShareTextAsync(
            $"tickets-{DateTime.Now:yyyyMMdd-HHmm}.html",
            TicketHtmlWriter.ToHtml(batch),
            "Exportar tickets a HTML");

        Success($"Se exportaron {batch.Tickets.Count} tickets a HTML.");
    });

    /// <summary>
    /// Devuelve el lote congelado al generarlo. Antes se reconstruía con el perfil y los datos
    /// de la tarjeta que hubiera en pantalla, así que cambiar el selector después de generar
    /// alteraba el CSV y el PDF de un lote ya creado.
    /// </summary>
    private TicketBatchExport CurrentBatch()
    {
        if (_currentBatch is null || Generated.Count == 0)
            throw new InvalidOperationException("Genera un lote antes de exportarlo.");

        return _currentBatch;
    }

    private Task DeleteUserAsync(HotspotUser? user) => RunAsync(async () =>
    {
        if (user is null)
            return;

        if (await _router.RemoveUsersAsync([user.Name]) == 0)
            throw new InvalidOperationException($"El ticket «{user.Name}» ya no existe en el router.");

        Success($"Ticket «{user.Name}» eliminado.");
        await LoadUsersAsync();
    });

    private Task DeleteSelectedAsync() => RunAsync(async () =>
    {
        var user = SelectedUser;
        if (user is null)
        {
            Info("Selecciona un ticket de la lista.");
            return;
        }

        if (await _router.RemoveUsersAsync([user.Name]) == 0)
            throw new InvalidOperationException($"El ticket «{user.Name}» ya no existe en el router.");

        SelectedUser = null;
        Success($"Ticket «{user.Name}» eliminado.");
        await LoadUsersAsync();
    });

    private Task RemoveExpiredAsync() => RunAsync(async () =>
    {
        var expired = Users.Where(u => u.IsExpired).Select(u => u.Name).ToList();
        if (expired.Count == 0)
        {
            Info("No hay tickets agotados.");
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Limpiar agotados",
            $"Se eliminarán {expired.Count} tickets agotados del router. Esta acción no se puede deshacer.",
            "Eliminar", "Cancelar");
        if (!confirmed)
            return;

        // Un único listado y un único borrado por lotes, por muchos que haya agotados.
        var removed = await _router.RemoveUsersAsync(expired);

        await LoadUsersAsync();
        Success(removed == expired.Count
            ? $"Se eliminaron {removed} tickets agotados."
            : $"Se eliminaron {removed} de {expired.Count}; el resto ya no existía en el router.");
    });

    private async Task LoadUsersAsync()
    {
        var users = await _router.GetUsersAsync();
        Users.Clear();
        foreach (var user in users.OrderBy(u => u.Name))
            Users.Add(user);
        SelectedUser = Users.FirstOrDefault(u => u.Name == SelectedUser?.Name);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = Filter.Trim();
        var query = Users.AsEnumerable();

        if (OnlyExpired)
            query = query.Where(u => u.State is TicketState.Expired);
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
        ? "Sin tickets en el router."
        : $"{Users.Count} tickets · {Users.Count(u => u.State is TicketState.Online)} en línea · " +
          $"{Users.Count(u => u.State is TicketState.Paused)} pausados · " +
          $"{Users.Count(u => u.State is TicketState.Available)} sin usar · " +
          $"{Users.Count(u => u.State is TicketState.Expired)} agotados";
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Models;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Administración diaria de tickets: ver quién está conectado, cuánto tiempo lleva y cortar
/// a quien se pasó. La lista se refresca sola para no depender de pulsar "Actualizar".
/// </summary>
public sealed class UsersViewModel : PageViewModel
{
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
    private CancellationTokenSource? _batchCancellation;

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
        ShareHtmlCommand = new AsyncCommand(ShareHtmlAsync);
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

    /// <summary>Datos opcionales que se imprimen en la tarjeta; se preguntan aquí y viajan con el lote.</summary>
    public string TicketBrand { get => _ticketBrand; set => Set(ref _ticketBrand, value); }
    public string TicketSsid { get => _ticketSsid; set => Set(ref _ticketSsid, value); }
    public string TicketPrice { get => _ticketPrice; set => Set(ref _ticketPrice, value); }
    public string TicketStatusUrl { get => _ticketStatusUrl; set => Set(ref _ticketStatusUrl, value); }

    public HotspotUser? SelectedUser { get => _selectedUser; set => Set(ref _selectedUser, value); }
    public HotspotProfile? SelectedProfile { get => _selectedProfile; set => Set(ref _selectedProfile, value); }

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
    public ICommand ShareHtmlCommand { get; }

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
                ?? throw new ArgumentException("Selecciona el perfil que usarán los tickets.");
            var duration = profile.TicketDuration
                ?? throw new ArgumentException($"El perfil «{profile.Name}» no tiene duración de ticket. " +
                                               "Créalo de nuevo indicando la duración, o usa otro perfil.");

            Generated.Clear();
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

            try
            {
                var created = await _provisioning.CreateBatchAsync(
                    count, profile.Name, duration, progress, _batchCancellation.Token);

                foreach (var ticket in created)
                    Generated.Add(ticket);

                await _history.SaveAsync(BuildBatch(profile.Name, duration, created));
                Success($"Se crearon {created.Count} tickets de {TicketBatchExport.Humanize(duration)}. " +
                        "Puedes exportarlos o imprimirlos.");
            }
            catch (OperationCanceledException)
            {
                // Lo que ya se creó antes de cancelar sigue siendo válido: se guarda igual.
                await PersistPartialAsync(profile.Name, duration);
                Warn($"Lote cancelado. Se conservaron {Generated.Count} tickets ya creados.");
            }
            catch (TicketBatchException partial)
            {
                foreach (var ticket in partial.Created)
                    Generated.Add(ticket);
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
        await _history.SaveAsync(BuildBatch(profile, duration, [.. Generated]));
    }

    /// <summary>
    /// Construye el lote con los datos opcionales de la tarjeta. El precio es texto libre:
    /// si no se entiende como número, es un error del formulario y no un lote sin precio.
    /// </summary>
    private TicketBatchExport BuildBatch(string profile, TimeSpan duration, IReadOnlyList<CreatedHotspotUser> tickets)
    {
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(TicketPrice))
        {
            if (!decimal.TryParse(TicketPrice, out var parsed) || parsed < 0 || parsed > 100_000)
                throw new ArgumentException("El precio debe ser un número positivo, por ejemplo 15.00.");
            price = parsed;
        }

        return new TicketBatchExport(
            DateTimeOffset.Now, profile, duration, tickets,
            NullIfEmpty(TicketBrand), NullIfEmpty(TicketSsid), price, NullIfEmpty(TicketStatusUrl));
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
    /// Construye el lote a partir de lo que hay en pantalla. Se exige que exista algo generado
    /// para no exportar un archivo vacío.
    /// </summary>
    private TicketBatchExport CurrentBatch()
    {
        if (Generated.Count == 0)
            throw new InvalidOperationException("Genera un lote antes de exportarlo.");

        var profile = SelectedProfile
            ?? throw new InvalidOperationException("Selecciona el perfil del lote.");

        return BuildBatch(
            profile.Name,
            profile.TicketDuration ?? TimeSpan.FromHours(1),
            [.. Generated]);
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
        var user = SelectedUser ?? throw new ArgumentException("Selecciona un ticket de la lista.");
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
        : $"{Users.Count} tickets · {Users.Count(u => u.State is TicketState.InUse)} en uso · " +
          $"{Users.Count(u => u.State is TicketState.Available)} sin usar · " +
          $"{Users.Count(u => u.State is TicketState.Expired)} agotados";
}

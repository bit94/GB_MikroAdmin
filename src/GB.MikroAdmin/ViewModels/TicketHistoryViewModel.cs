using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Histórico de lotes generados. Antes, cerrar la aplicación perdía los códigos recién creados
/// aunque siguieran activos en el router, y no había forma de recuperarlos ni reimprimirlos.
/// </summary>
public sealed class TicketHistoryViewModel : PageViewModel
{
    private readonly ITicketHistoryStore _history;
    private readonly IFileExporter _exporter;

    public TicketHistoryViewModel(
        ITicketHistoryStore history,
        IFileExporter exporter,
        ConnectionState connection) : base(connection)
    {
        _history = history;
        _exporter = exporter;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        PrintBatchCommand = new AsyncCommand<BatchRow>(PrintAsync);
        ThermalBatchCommand = new AsyncCommand<BatchRow>(PrintNextAsync);
        ExportBatchCommand = new AsyncCommand<BatchRow>(ExportAsync);
        DeleteBatchCommand = new AsyncCommand<BatchRow>(DeleteAsync);
    }

    public ObservableCollection<BatchRow> Batches { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand PrintBatchCommand { get; }
    public ICommand ThermalBatchCommand { get; }
    public ICommand ExportBatchCommand { get; }
    public ICommand DeleteBatchCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var batches = await _history.GetAllAsync();
        Batches.Clear();
        foreach (var batch in batches)
            Batches.Add(new BatchRow(batch));

        if (batches.Count == 0)
            Info("Todavía no hay lotes guardados.");
    });

    private Task PrintAsync(BatchRow? row) => RunAsync(async () =>
    {
        if (row is null)
            return;

        await _exporter.PrintAsync(
            $"tickets-{row.Batch.CreatedAt:yyyyMMdd-HHmm}.pdf",
            TicketPdfWriter.WriteSheet(row.Batch),
            $"Tickets {row.Batch.Profile}");

        // No hay forma de saber si la impresión se completó: se registra al lanzarla, que es
        // el compromiso habitual en aplicaciones de mostrador.
        await MarkPrintedAsync(row, row.Batch.Tickets.Select(t => t.Name).ToList());
        Success("PDF generado. Elige la impresora en la ventana que se abrió.");
    });

    /// <summary>Imprime el siguiente ticket del lote sin imprimir, en formato térmico de 80 mm.</summary>
    private Task PrintNextAsync(BatchRow? row) => RunAsync(async () =>
    {
        if (row is null)
            return;

        var ticket = row.NextUnprinted
            ?? throw new InvalidOperationException("Todos los tickets de este lote ya se imprimieron.");

        await _exporter.PrintAsync(
            $"ticket-{ticket.Name}.pdf",
            TicketPdfWriter.WriteThermal(row.Batch, ticket),
            $"Ticket {ticket.Name}");

        await MarkPrintedAsync(row, [ticket.Name]);
        Success($"Ticket {ticket.Name} enviado a la impresora térmica.");
    });

    private async Task MarkPrintedAsync(BatchRow row, IReadOnlyCollection<string> codes)
    {
        await _history.MarkPrintedAsync(row.Batch, codes);

        var index = Batches.IndexOf(row);
        if (index >= 0)
            Batches[index] = row with { Batch = row.Batch with { Printed = row.Batch.Printed.Union(codes).ToList() } };
    }

    private Task ExportAsync(BatchRow? row) => RunAsync(async () =>
    {
        if (row is null)
            return;

        await _exporter.ShareTextAsync(
            $"tickets-{row.Batch.CreatedAt:yyyyMMdd-HHmm}.csv",
            TicketExporter.ToCsv(row.Batch),
            "Exportar tickets a CSV");

        Success($"Se exportaron {row.Batch.Tickets.Count} tickets.");
    });

    private Task DeleteAsync(BatchRow? row) => RunAsync(async () =>
    {
        if (row is null)
            return;

        await _history.RemoveAsync(row.Batch);
        Batches.Remove(row);
        Success("Lote quitado del histórico. Los usuarios siguen creados en el router.");
    });

    /// <summary>Fila del histórico: el lote y sus códigos ya formateados para leer o copiar.</summary>
    public sealed record BatchRow(TicketBatchExport Batch)
    {
        public string Title =>
            $"{Batch.CreatedAt:dd/MM/yyyy HH:mm} · {Batch.Tickets.Count} tickets · " +
            $"{Batch.Profile} · {Batch.DurationText}";

        /// <summary>Seguimiento de impresión: «Impresos 3/10». Los lotes anteriores a este
        /// seguimiento empiezan en cero aunque se hubieran impreso.</summary>
        public string PrintedSummary => $"Impresos {Batch.Printed.Count}/{Batch.Tickets.Count}";

        /// <summary>Primer ticket del lote que aún no se ha enviado a imprimir.</summary>
        public CreatedHotspotUser? NextUnprinted =>
            Batch.Tickets.FirstOrDefault(t => !Batch.Printed.Contains(t.Name));

        /// <summary>Códigos separados por espacios, para poder copiarlos de una vez.</summary>
        public string Codes => string.Join("   ", Batch.Tickets.Select(v => v.Name));
    }
}

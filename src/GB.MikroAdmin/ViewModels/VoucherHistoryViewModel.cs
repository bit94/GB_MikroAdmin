using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Core.Services;
using GB.MikroAdmin.Services;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Histórico de lotes generados. Antes, cerrar la aplicación perdía los códigos recién creados
/// aunque siguieran activos en el router, y no había forma de recuperarlos ni reimprimirlos.
/// </summary>
public sealed class VoucherHistoryViewModel : PageViewModel
{
    private readonly IVoucherHistoryStore _history;
    private readonly IFileExporter _exporter;

    public VoucherHistoryViewModel(
        IVoucherHistoryStore history,
        IFileExporter exporter,
        ConnectionState connection) : base(connection)
    {
        _history = history;
        _exporter = exporter;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        PrintBatchCommand = new AsyncCommand<BatchRow>(PrintAsync);
        ExportBatchCommand = new AsyncCommand<BatchRow>(ExportAsync);
        DeleteBatchCommand = new AsyncCommand<BatchRow>(DeleteAsync);
    }

    public ObservableCollection<BatchRow> Batches { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand PrintBatchCommand { get; }
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
            $"vouchers-{row.Batch.CreatedAt:yyyyMMdd-HHmm}.pdf",
            VoucherPdfWriter.Write(row.Batch),
            $"Vouchers {row.Batch.Profile}");

        Success("PDF generado. Elige la impresora en la ventana que se abrió.");
    });

    private Task ExportAsync(BatchRow? row) => RunAsync(async () =>
    {
        if (row is null)
            return;

        await _exporter.ShareTextAsync(
            $"vouchers-{row.Batch.CreatedAt:yyyyMMdd-HHmm}.csv",
            VoucherExporter.ToCsv(row.Batch),
            "Exportar vouchers a CSV");

        Success($"Se exportaron {row.Batch.Vouchers.Count} vouchers.");
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
    public sealed record BatchRow(VoucherBatchExport Batch)
    {
        public string Title =>
            $"{Batch.CreatedAt:dd/MM/yyyy HH:mm} · {Batch.Vouchers.Count} vouchers · " +
            $"{Batch.Profile} · {Batch.DurationText}";

        /// <summary>Códigos separados por espacios, para poder copiarlos de una vez.</summary>
        public string Codes => string.Join("   ", Batch.Vouchers.Select(v => v.Name));
    }
}

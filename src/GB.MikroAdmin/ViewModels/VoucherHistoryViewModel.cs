using System.Collections.ObjectModel;
using System.Windows.Input;
using GB.MikroAdmin.Views;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Histórico de lotes generados. Antes, cerrar la aplicación perdía los códigos recién creados
/// aunque siguieran activos en el router, y no había forma de recuperarlos ni reimprimirlos.
/// </summary>
public sealed class VoucherHistoryViewModel : PageViewModel
{
    private readonly IVoucherHistoryStore _history;

    public VoucherHistoryViewModel(IVoucherHistoryStore history, ConnectionState connection) : base(connection)
    {
        _history = history;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public ObservableCollection<BatchRow> Batches { get; } = [];

    public ICommand RefreshCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var batches = await _history.GetAllAsync();
        Batches.Clear();
        foreach (var batch in batches)
            Batches.Add(new BatchRow(batch));

        if (batches.Count == 0)
            Info("Todavía no hay lotes guardados.");
    });

    /// <summary>Fila del histórico: el lote y sus códigos ya formateados para leer o imprimir.</summary>
    public sealed record BatchRow(VoucherBatch Batch)
    {
        public string Title => Batch.Title;

        /// <summary>Códigos en columnas para que se puedan copiar de una vez.</summary>
        public string Codes => string.Join("   ", Batch.Vouchers.Select(v => v.Name));
    }
}

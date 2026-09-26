using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GB.MikroAdmin.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Comando asíncrono que se deshabilita mientras se ejecuta y expone el fallo en lugar de
/// dejarlo escapar: un <c>async void</c> con excepción sin control derriba el proceso.
/// </summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private bool _busy;

    public AsyncCommand(Func<Task> execute) : this(_ => execute()) { }

    public AsyncCommand(Func<object?, Task> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    /// <summary>Se dispara cuando la tarea del comando termina en excepción no controlada.</summary>
    public event EventHandler<Exception>? ExecutionFailed;

    public bool IsRunning => _busy;

    public bool CanExecute(object? parameter) => !_busy;

    public async void Execute(object? parameter)
    {
        if (_busy)
            return;

        _busy = true;
        Raise();
        try
        {
            await _execute(parameter);
        }
        catch (Exception exception)
        {
            ExecutionFailed?.Invoke(this, exception);
        }
        finally
        {
            _busy = false;
            Raise();
        }
    }

    private void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

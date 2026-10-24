using System.Windows.Input;
using GB.MikroAdmin.Guide;
using Microsoft.Maui.Storage;

namespace GB.MikroAdmin.ViewModels;

/// <summary>
/// Estado de la guía de instalación: capítulo abierto, pasos marcados y aviso de "copiado".
/// El avance se guarda en preferencias para poder retomar la instalación otro día.
/// </summary>
public sealed class GuideViewModel : ObservableObject
{
    private const string ProgressKey = "guide.progress.v1";

    private readonly HashSet<string> _done = new(StringComparer.Ordinal);
    private bool _showCopied;
    private int _copyVersion;

    public GuideViewModel()
    {
        Chapters = GuideContent.Build();

        foreach (var id in Preferences.Default.Get(ProgressKey, string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries))
            _done.Add(id);

        foreach (var chapter in Chapters)
            foreach (var step in chapter.Steps)
                step.IsDone = _done.Contains(step.Number);

        // Se abre el capítulo donde quedó el trabajo: al empezar es el 0; al retomar, el
        // primero con pasos pendientes, para no buscar a mano dónde se quedó.
        var current = Chapters.FirstOrDefault(chapter => !chapter.IsComplete) ?? Chapters.FirstOrDefault();
        if (current is not null)
            current.IsExpanded = true;

        ExpandAllCommand = new AsyncCommand(async () =>
        {
            foreach (var chapter in Chapters)
            {
                if (chapter.IsExpanded)
                    continue;

                chapter.IsExpanded = true;
                // Crear los 100 pasos de golpe bloquea la interfaz en Android: se cede el
                // hilo entre capítulos para que la pantalla siga respondiendo.
                await Task.Delay(40);
            }
        });
        CollapseAllCommand = new AsyncCommand(() =>
        {
            foreach (var chapter in Chapters)
                chapter.IsExpanded = false;
            return Task.CompletedTask;
        });
        CopyCommand = new AsyncCommand<string>(CopyAsync);
        ToggleStepCommand = new AsyncCommand<GuideStep>(ToggleStepAsync);
        ResetProgressCommand = new AsyncCommand(ResetProgressAsync);
    }

    public IReadOnlyList<GuideChapter> Chapters { get; }

    public string Subtitle => "De Starlink al ticket del cliente, paso a paso.";
    public string Stats => $"{Chapters.Count} capítulos · {TotalCount} pasos · 60–120 min";
    public string Intro =>
        "Cada capítulo se abre con un toque. Toca el círculo de un paso para marcarlo como " +
        "hecho: el avance se guarda solo y puedes retomarlo otro día. Los bloques con fondo " +
        "oscuro son comandos de RouterOS que puedes copiar con «Copiar»; los bloques «EJEMPLO» " +
        "muestran valores concretos para que no tengas que inventarlos.";
    public string Warning =>
        "Reiniciar el hAP lite borra usuarios, perfiles, certificados y configuración. " +
        "Los tickets activos dejan de funcionar en ese momento. Hazlo fuera de horario y avisa a tus clientes.";

    public int TotalCount => Chapters.Sum(chapter => chapter.Steps.Count);
    public int CompletedCount => Chapters.Sum(chapter => chapter.Steps.Count(step => step.IsDone));
    public double Progress => TotalCount == 0 ? 0 : (double)CompletedCount / TotalCount;
    public bool HasProgress => CompletedCount > 0;
    public string ProgressText => $"{CompletedCount} de {TotalCount} pasos";
    public string ProgressHint => CompletedCount == 0
        ? "Marca cada paso al terminarlo; el avance se guarda en este dispositivo."
        : CompletedCount == TotalCount
            ? "Guía completada. Guarda un respaldo del router y anota el vencimiento del certificado."
            : $"Te faltan {TotalCount - CompletedCount} pasos. El avance se guarda solo.";

    public bool ShowCopied { get => _showCopied; private set => Set(ref _showCopied, value); }

    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ToggleStepCommand { get; }
    public ICommand ResetProgressCommand { get; }

    private Task ToggleStepAsync(GuideStep? step)
    {
        if (step is null)
            return Task.CompletedTask;

        step.IsDone = !step.IsDone;
        if (step.IsDone)
            _done.Add(step.Number);
        else
            _done.Remove(step.Number);

        Preferences.Default.Set(ProgressKey, string.Join(',', _done.OrderBy(id => id, StringComparer.Ordinal)));

        Raise(nameof(CompletedCount));
        Raise(nameof(HasProgress));
        Raise(nameof(Progress));
        Raise(nameof(ProgressText));
        Raise(nameof(ProgressHint));
        return Task.CompletedTask;
    }

    private async Task ResetProgressAsync()
    {
        if (!HasProgress)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Reiniciar avance",
            "Se desmarcarán todos los pasos de la guía en este dispositivo. La configuración del router no se toca.",
            "Reiniciar",
            "Cancelar");
        if (!confirmed)
            return;

        foreach (var chapter in Chapters)
            foreach (var step in chapter.Steps)
                step.IsDone = false;

        _done.Clear();
        Preferences.Default.Remove(ProgressKey);

        Raise(nameof(CompletedCount));
        Raise(nameof(HasProgress));
        Raise(nameof(Progress));
        Raise(nameof(ProgressText));
        Raise(nameof(ProgressHint));
    }

    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(text);

        var version = ++_copyVersion;
        ShowCopied = true;
        await Task.Delay(1600);
        if (version == _copyVersion)
            ShowCopied = false;
    }
}

public enum GuideNoteKind
{
    None,
    Tip,
    Warning
}

/// <summary>Un paso concreto dentro de un capítulo.</summary>
public sealed class GuideStep : ObservableObject
{
    private bool _isDone;

    public GuideStep(
        string number,
        string title,
        string body,
        string? code = null,
        string? example = null,
        GuideNoteKind noteKind = GuideNoteKind.None,
        string? note = null)
    {
        Number = number;
        Title = title;
        Body = body;
        Code = code;
        Example = example;
        NoteKind = noteKind;
        Note = note ?? string.Empty;
    }

    public string Number { get; }
    public string Title { get; }
    public string Body { get; }
    public string? Code { get; }
    public string? Example { get; }
    public GuideNoteKind NoteKind { get; }
    public string Note { get; }

    public bool HasCode => !string.IsNullOrWhiteSpace(Code);
    public bool HasExample => !string.IsNullOrWhiteSpace(Example);
    public bool IsTip => NoteKind == GuideNoteKind.Tip;
    public bool IsWarning => NoteKind == GuideNoteKind.Warning;

    /// <summary>Pasos completados. El número es único (capítulo.paso) y sirve como clave guardada.</summary>
    public bool IsDone
    {
        get => _isDone;
        set
        {
            if (Set(ref _isDone, value))
            {
                Raise(nameof(IsNotDone));
                Raise(nameof(CheckDescription));
            }
        }
    }

    public bool IsNotDone => !IsDone;
    public string CheckDescription => IsDone ? $"Desmarcar paso {Number}" : $"Marcar paso {Number} como hecho";
}

/// <summary>Capítulo plegable de la guía.</summary>
public sealed class GuideChapter : ObservableObject
{
    private bool _isExpanded;

    public GuideChapter(string number, string title, string summary, IReadOnlyList<GuideStep> steps)
    {
        Number = number;
        Title = title;
        Summary = summary;
        Steps = steps;
        ToggleCommand = new AsyncCommand(() =>
        {
            IsExpanded = !IsExpanded;
            return Task.CompletedTask;
        });

        foreach (var step in steps)
            step.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(GuideStep.IsDone))
                    RefreshProgress();
            };
    }

    public string Number { get; }
    public string Title { get; }
    public string Summary { get; }
    public IReadOnlyList<GuideStep> Steps { get; }

    /// <summary>
    /// BindableLayout no virtualiza: si se enlazan todos los pasos, Android crea cientos de
    /// vistas al abrir la guía. Al plegar no se enlaza nada; al abrir se crean solo esos pasos.
    /// </summary>
    public IReadOnlyList<GuideStep> VisibleSteps => IsExpanded ? Steps : Array.Empty<GuideStep>();
    public int DoneCount => Steps.Count(step => step.IsDone);
    public bool IsComplete => Steps.Count > 0 && DoneCount == Steps.Count;
    public double Progress => Steps.Count == 0 ? 0 : (double)DoneCount / Steps.Count;
    public string ProgressText => IsComplete
        ? $"{Steps.Count} pasos · completado"
        : $"{DoneCount} de {Steps.Count} pasos";

    private void RefreshProgress()
    {
        Raise(nameof(DoneCount));
        Raise(nameof(IsComplete));
        Raise(nameof(Progress));
        Raise(nameof(ProgressText));
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value))
            {
                Raise(nameof(ToggleGlyph));
                Raise(nameof(VisibleSteps));
            }
        }
    }

    public string ToggleGlyph => IsExpanded ? "−" : "+";
    public ICommand ToggleCommand { get; }
}

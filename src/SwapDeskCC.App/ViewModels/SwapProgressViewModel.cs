using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwapDeskCC.Core.Models;

namespace SwapDeskCC.App.ViewModels;

public sealed partial class StepViewModel(string title, string? note) : ObservableObject
{
    [ObservableProperty] private string _title = title;
    [ObservableProperty] private string? _note = note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon), nameof(IsRunning), nameof(IsDone), nameof(IsFailed), nameof(IsPending), nameof(HasNote))]
    private SwapStepState _state = SwapStepState.Pending;

    public string Icon => State switch
    {
        SwapStepState.Running => "◌",
        SwapStepState.Done => "✓",
        SwapStepState.Failed => "✕",
        _ => "○",
    };

    public bool IsPending => State == SwapStepState.Pending;
    public bool IsRunning => State == SwapStepState.Running;
    public bool IsDone => State == SwapStepState.Done;
    public bool IsFailed => State == SwapStepState.Failed;
    public bool HasNote => !string.IsNullOrEmpty(Note);

    partial void OnNoteChanged(string? value) => OnPropertyChanged(nameof(HasNote));
}

/// <summary>Janela de etapas da troca (overlay da janela principal).</summary>
public sealed partial class SwapProgressViewModel : ObservableObject
{
    public ObservableCollection<StepViewModel> Steps { get; } = [];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public void Start(string title, IEnumerable<SwapStep> steps)
    {
        Title = title;
        Error = null;
        Progress = 0;
        Steps.Clear();
        foreach (var s in steps) Steps.Add(new StepViewModel(s.Title, s.Note));
        IsOpen = true;
    }

    /// <summary>Recebe as notificações do motor. Cria a etapa se ela não estava na pré-visualização.</summary>
    public void Apply(SwapStep step)
    {
        while (Steps.Count <= step.Index) Steps.Add(new StepViewModel(step.Title, step.Note));
        var vm = Steps[step.Index];
        vm.Title = step.Title;
        vm.Note = step.Note;
        vm.State = step.State;
        if (step.State == SwapStepState.Done) Progress = (double)(step.Index + 1) / Steps.Count;
    }

    public void Fail(string error) => Error = error;

    [RelayCommand]
    private void Close() => IsOpen = false;
}

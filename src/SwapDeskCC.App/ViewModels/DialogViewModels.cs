using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwapDeskCC.Core.Models;
using ColorFormat = SwapDeskCC.Core.Models.ColorText;

namespace SwapDeskCC.App.ViewModels;

public sealed partial class ColorChoice(string hex, AccountEditorViewModel owner) : ObservableObject
{
    public string Hex { get; } = hex;
    public IBrush Brush { get; } = Avalonia.Media.Brush.Parse(hex);

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Select() => owner.SelectColor(this);
}

/// <summary>Diálogo de conta: "Adicionar conta" (assistente) e "Editar conta" (apelido, e-mail e cor).</summary>
public sealed partial class AccountEditorViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private string? _editingId;
    private string _selectedColor = Account.Palette[0];

    public AccountEditorViewModel(MainViewModel main)
    {
        _main = main;
        foreach (var hex in Account.Palette) Colors.Add(new ColorChoice(hex, this));
    }

    public ObservableCollection<ColorChoice> Colors { get; } = [];

    [ObservableProperty] private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _name = "";

    [ObservableProperty] private string _email = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmText))]
    private bool _isSimulation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ConfirmText), nameof(IsAdding))]
    private bool _isEditing;

    /// <summary>Texto livre da cor: rgba(r, g, b, a), rgb(...) ou #hex.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _colorText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _isColorInvalid;

    [ObservableProperty] private IBrush _previewBrush = Brush.Parse(Account.Palette[0]);

    public string Title => IsEditing ? "Editar conta" : "Adicionar conta";
    public bool IsAdding => !IsEditing;
    public string ConfirmText => IsEditing ? "Salvar" : IsSimulation ? "Concluir" : "Criar e abrir o Claude";
    public string Initials => new Account { Name = Name }.Initials;

    public void OpenForAdd(bool simulation, int accountCount)
    {
        _editingId = null;
        IsEditing = false;
        IsSimulation = simulation;
        Name = "";
        Email = "";
        SelectColor(Colors[accountCount % Colors.Count]);
        IsOpen = true;
    }

    public void OpenForEdit(Account account)
    {
        _editingId = account.Id;
        IsEditing = true;
        Name = account.Name;
        Email = account.Email;
        ColorText = ColorFormat.ToRgba(account.Color);
        IsOpen = true;
    }

    public void SelectColor(ColorChoice choice) => ColorText = ColorFormat.ToRgba(choice.Hex);

    partial void OnColorTextChanged(string value)
    {
        IsColorInvalid = !ColorFormat.TryParseInput(value, out var stored);
        if (IsColorInvalid) return;

        _selectedColor = stored;
        PreviewBrush = Brush.Parse(stored);
        foreach (var c in Colors) c.IsSelected = ColorFormat.SameColor(c.Hex, stored);
    }

    private bool CanConfirm() => !string.IsNullOrWhiteSpace(Name) && !IsColorInvalid;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task Confirm()
    {
        IsOpen = false;
        if (_editingId is { } id)
            _main.UpdateAccount(id, Name.Trim(), Email.Trim(), _selectedColor);
        else
            await _main.AddAccountAsync(Name.Trim(), Email.Trim(), _selectedColor);
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;
}

/// <summary>Diálogo genérico de confirmação (ex.: ativar a troca real).</summary>
public sealed partial class ConfirmViewModel : ObservableObject
{
    private TaskCompletionSource<bool>? _tcs;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _confirmText = "Confirmar";

    public Task<bool> AskAsync(string title, string message, string confirmText)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        _tcs = new TaskCompletionSource<bool>();
        IsOpen = true;
        return _tcs.Task;
    }

    [RelayCommand]
    private void Confirm() => Finish(true);

    [RelayCommand]
    private void Cancel() => Finish(false);

    private void Finish(bool result)
    {
        IsOpen = false;
        _tcs?.TrySetResult(result);
    }
}

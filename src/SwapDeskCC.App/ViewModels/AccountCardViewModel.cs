using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwapDeskCC.Core.Models;

namespace SwapDeskCC.App.ViewModels;

public sealed partial class AccountCardViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public AccountCardViewModel(MainViewModel main, Account account, string codeDirDisplay)
    {
        _main = main;
        Account = account;
        CodeLabel = $"Code: {codeDirDisplay}";
    }

    public Account Account { get; }
    public string Name => Account.Name;
    public string Email => string.IsNullOrWhiteSpace(Account.Email) ? "—" : Account.Email;
    public string Initials => Account.Initials;
    public string Plan => Account.Plan;
    public bool HasPlan => !string.IsNullOrWhiteSpace(Account.Plan);
    public string CodeLabel { get; }
    public IBrush ColorBrush => Brush.Parse(Account.Color);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText), nameof(CanUse), nameof(CanRemove))]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUse))]
    private bool _isBusy;

    public string ButtonText => IsActive ? "Em uso" : "Usar esta conta";
    public bool CanUse => !IsActive && !IsBusy;
    public bool CanRemove => !IsActive;

    [RelayCommand]
    private Task Use() => _main.SwapToAsync(Account.Id);

    [RelayCommand]
    private void Edit() => _main.EditAccount(Account.Id);

    [RelayCommand]
    private void Remove() => _main.RemoveAccount(Account.Id);
}

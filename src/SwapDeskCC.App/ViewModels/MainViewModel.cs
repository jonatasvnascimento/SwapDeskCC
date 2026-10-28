using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwapDeskCC.App.Services;
using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.App.ViewModels;

public sealed record ActivityItem(string Time, string Text);

/// <param name="Sticky">Fica na tela até o próximo aviso (usado no progresso quando a janela está escondida).</param>
public sealed record ToastRequest(string Title, string Subtitle, string Initials, string Color, bool Sticky = false, bool IsError = false);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppHost _host;

    public MainViewModel(AppHost host)
    {
        _host = host;
        Editor = new AccountEditorViewModel(this);
        Settings = new SettingsViewModel(this, host);
        Refresh();
    }

    public AppHost Host => _host;
    public AppConfig Config => _host.Config;

    /// <summary>Avisa a bandeja para refazer o menu e o ícone.</summary>
    public event Action? StateChanged;

    public event Action<ToastRequest>? ToastRequested;

    /// <summary>Preenchido pelo App: se a janela está escondida, o progresso aparece no toast.</summary>
    public Func<bool>? IsWindowVisible { get; set; }

    public ObservableCollection<AccountCardViewModel> Cards { get; } = [];
    public ObservableCollection<ActivityItem> Activity { get; } = [];
    public SwapProgressViewModel Swap { get; } = new();
    public AccountEditorViewModel Editor { get; }
    public ConfirmViewModel Confirm { get; } = new();
    public SettingsViewModel Settings { get; }

    // ---------- Navegação ----------

    [ObservableProperty] private bool _isAccountsPage = true;
    [ObservableProperty] private bool _isSettingsPage;
    [ObservableProperty] private bool _isAboutPage;

    // ---------- Banner "Ativa agora" ----------

    [ObservableProperty] private string _activeName = "";
    [ObservableProperty] private string _activeEmail = "";
    [ObservableProperty] private IBrush _activeBrush = Brushes.Gray;
    [ObservableProperty] private bool _isSimulation;
    [ObservableProperty] private bool _isSwapping;

    public bool HasActivity => Activity.Count > 0;

    public string DesktopPillText => _host.Install is null
        ? "Desktop não encontrado"
        : _host.Platform.DesktopOfficial ? "Desktop ✓" : "Desktop experimental";

    public bool DesktopPillOk => _host.Install is not null && _host.Platform.DesktopOfficial;

    // ---------- "Trocar junto" ----------

    public bool IsScopeBoth
    {
        get => Config.Scope == SwapScope.Both;
        set { if (value) SetScope(SwapScope.Both); }
    }

    public bool IsScopeDesktop
    {
        get => Config.Scope == SwapScope.DesktopOnly;
        set { if (value) SetScope(SwapScope.DesktopOnly); }
    }

    public bool IsScopeCode
    {
        get => Config.Scope == SwapScope.CodeOnly;
        set { if (value) SetScope(SwapScope.CodeOnly); }
    }

    public string ScopeLabel => Config.Scope.Label();

    private void SetScope(SwapScope scope)
    {
        if (Config.Scope == scope) return;
        Config.Scope = scope;
        _host.Save();
        OnPropertyChanged(nameof(IsScopeBoth));
        OnPropertyChanged(nameof(IsScopeDesktop));
        OnPropertyChanged(nameof(IsScopeCode));
        OnPropertyChanged(nameof(ScopeLabel));
        StateChanged?.Invoke();
    }

    // ---------- Estado ----------

    public void Refresh()
    {
        var active = Config.ActiveAccount ?? Config.Accounts[0];
        ActiveName = active.Name;
        ActiveEmail = string.IsNullOrWhiteSpace(active.Email) ? "" : $"· {active.Email}";
        ActiveBrush = Brush.Parse(active.Color);
        IsSimulation = Config.Settings.SimulationMode;

        var engine = _host.CreateEngine();
        Cards.Clear();
        foreach (var account in Config.Accounts)
        {
            Cards.Add(new AccountCardViewModel(this, account, engine.Operations.CodeDirDisplay(account))
            {
                IsActive = account.Id == active.Id,
                IsBusy = IsSwapping,
            });
        }

        Activity.Clear();
        foreach (var entry in Config.Activity) Activity.Add(new ActivityItem(entry.At.ToString("HH:mm"), entry.Text));
        OnPropertyChanged(nameof(HasActivity));

        Settings.RefreshInfo();
        StateChanged?.Invoke();
    }

    partial void OnIsSwappingChanged(bool value)
    {
        foreach (var card in Cards) card.IsBusy = value;
    }

    private void Toast(ToastRequest request) => ToastRequested?.Invoke(request);

    private void ToastFor(Account account, string title, string subtitle, bool sticky = false) =>
        Toast(new ToastRequest(title, subtitle, account.Initials, account.Color, sticky));

    private void ToastError(string title, string subtitle) =>
        Toast(new ToastRequest(title, subtitle, "!", "#c42b1c", IsError: true));

    // ---------- Troca ----------

    public async Task SwapToAsync(string id)
    {
        var from = Config.ActiveAccount;
        var to = Config.Accounts.FirstOrDefault(a => a.Id == id);
        if (IsSwapping || from is null || to is null || from.Id == to.Id) return;

        var scope = Config.Scope;
        if (!Config.Settings.SimulationMode && _host.Install is null && scope.IncludesDesktop())
        {
            ToastError("Claude Desktop não encontrado", "Escolha \"Só Code\" para trocar só o Claude Code.");
            return;
        }

        var engine = _host.CreateEngine();
        var windowVisible = IsWindowVisible?.Invoke() ?? true;
        IsSwapping = true;
        Swap.Start($"Trocando para {to.Name}…", engine.Describe(from, to, scope));
        if (!windowVisible) Swap.IsOpen = false;

        var progress = new Progress<SwapStep>(step =>
        {
            Swap.Apply(step);
            if (!windowVisible && step.State == SwapStepState.Running)
                ToastFor(to, $"Trocando para {to.Name}…", step.Title, sticky: true);
        });

        var result = await engine.SwapAsync(from, to, scope, progress);
        IsSwapping = false;

        if (!result.Success)
        {
            Swap.Fail(result.Error ?? "Erro desconhecido.");
            Config.AddActivity($"Falha ao trocar para {to.Name}: {result.Error}");
            _host.Save();
            Refresh();
            if (!windowVisible) ToastError($"Não foi possível trocar para {to.Name}", result.Error ?? "");
            return;
        }

        Config.ActiveId = to.Id;
        Config.AddActivity($"{from.Name} → {to.Name}  ({scope.Label()})");
        _host.Save();

        await Task.Delay(400);
        Swap.IsOpen = false;
        Refresh();

        if (Config.Settings.NotifyOnSwap || !windowVisible)
        {
            var who = string.IsNullOrWhiteSpace(to.Email) ? to.Name : to.Email;
            ToastFor(to, $"Conta trocada: {to.Name}", $"{scope.Label()} já usando {who}");
        }
    }

    // ---------- Contas ----------

    [RelayCommand]
    private void OpenAddAccount() => Editor.OpenForAdd(Config.Settings.SimulationMode, Config.Accounts.Count);

    public void EditAccount(string id)
    {
        if (Config.Accounts.FirstOrDefault(a => a.Id == id) is { } account) Editor.OpenForEdit(account);
    }

    /// <summary>Muda só o que aparece na tela. O id (e as pastas de perfil) continua o mesmo.</summary>
    public void UpdateAccount(string id, string name, string email, string color)
    {
        var account = Config.Accounts.FirstOrDefault(a => a.Id == id);
        if (account is null) return;

        if (account.Name != name) Config.AddActivity($"Conta {account.Name} renomeada para {name}");
        else Config.AddActivity($"Conta {name} editada");

        account.Name = name;
        account.Email = email;
        account.Color = color;
        _host.Save();
        Refresh();
    }

    public async Task AddAccountAsync(string name, string email, string color)
    {
        var id = Config.NewAccountId(name);
        var account = new Account
        {
            Id = id,
            Name = name,
            Email = email,
            Color = color,
            CodeConfigDir = _host.NewCodeConfigDir(id),
        };
        Config.Accounts.Add(account);
        Config.AddActivity($"Conta {name} adicionada");
        _host.Save();
        Refresh();

        if (Config.Settings.SimulationMode) return;

        // Modo real: troca para o perfil vazio, e o Claude abre na tela de login.
        await SwapToAsync(id);
        if (Config.ActiveId == id)
            ToastFor(account, $"Faça login como {name}", "No Claude Desktop, e no terminal rode: claude /login");
    }

    public async void RemoveAccount(string id)
    {
        var account = Config.Accounts.FirstOrDefault(a => a.Id == id);
        if (account is null || account.Id == Config.ActiveId) return;

        var ok = await Confirm.AskAsync(
            $"Remover \"{account.Name}\"?",
            "A conta sai da lista do SwapDeskCC. Nenhum arquivo é apagado: o perfil continua em\n"
                + PathDisplay.Shorten(Path.Combine(_host.ProfilesRoot, account.Id)),
            "Remover da lista");
        if (!ok) return;

        Config.Accounts.Remove(account);
        Config.AddActivity($"Conta {account.Name} removida da lista");
        _host.Save();
        Refresh();
    }

    // ---------- Modo real ----------

    /// <summary>
    /// Desliga a simulação. Na primeira vez, importa a pasta atual do Claude como perfil da conta ativa.
    /// Retorna false se o usuário desistiu ou a importação falhou (a simulação continua ligada).
    /// </summary>
    public async Task<bool> EnableRealModeAsync()
    {
        var active = Config.ActiveAccount ?? Config.Accounts[0];
        var profiles = _host.Profiles;

        if (_host.Platform.IsRunningInsidePackage)
        {
            await Confirm.AskAsync("Abra o SwapDeskCC por fora",
                "Este SwapDeskCC foi aberto de dentro de outro app empacotado (por exemplo, um terminal do Claude Desktop). "
                + "Nesse modo o Windows redireciona as pastas, e a troca real poderia mexer no lugar errado.\n\n"
                + "Feche-o e abra pelo Visual Studio, pelo Explorer ou pelo menu Iniciar.",
                "Entendi");
            return false;
        }

        if (_host.Install is null)
        {
            var go = await Confirm.AskAsync("Ativar troca real",
                "O Claude Desktop não foi encontrado neste computador.\nA troca real vai funcionar só para o Claude Code (CLAUDE_CONFIG_DIR).",
                "Ativar só para o Code");
            if (!go) return false;
        }
        else if (!Config.DesktopImported && profiles.IsImported)
        {
            Config.DesktopImported = true; // já era link (config antigo ou reinstalação do SwapDeskCC)
        }
        else if (!Config.DesktopImported)
        {
            var link = _host.Platform.LinkMethod;
            var go = await Confirm.AskAsync("Ativar troca real",
                "Para trocar de verdade, o SwapDeskCC vai:\n\n"
                + "•  fechar o Claude Desktop\n"
                + $"•  fazer um backup em {PathDisplay.Shorten(_host.BackupsRoot)}\n"
                + $"•  mover {PathDisplay.Shorten(_host.DataDir)} para o perfil \"{active.Name}\"\n"
                + $"•  deixar um {link} no lugar, apontando para o perfil\n\n"
                + "É um método não oficial: uma atualização do Claude pode mudar onde os dados ficam.",
                "Fechar o Claude e ativar");
            if (!go) return false;

            Config.Settings.SimulationMode = false;
            var engine = _host.CreateEngine();
            Swap.Start("Ativando troca real…", []);
            var result = await engine.ImportAsync(active, new Progress<SwapStep>(Swap.Apply));
            if (!result.Success)
            {
                Config.Settings.SimulationMode = true;
                Swap.Fail(result.Error ?? "Erro desconhecido.");
                _host.Save();
                Refresh();
                return false;
            }

            Config.DesktopImported = true;
            Config.AddActivity($"Troca real ativada · perfil \"{active.Name}\" importado");
            await Task.Delay(400);
            Swap.IsOpen = false;
        }

        Config.Settings.SimulationMode = false;
        _host.Save();
        Refresh();
        ToastFor(active, "Troca real ativada", "As próximas trocas mexem no Claude de verdade.");
        return true;
    }

    public void DisableRealMode()
    {
        Config.Settings.SimulationMode = true;
        _host.Save();
        Refresh();
    }
}

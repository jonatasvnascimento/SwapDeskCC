using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwapDeskCC.App.Services;
using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly AppHost _host;
    private bool _busy;

    // Valores exibidos nas caixas que podem ser recusadas (cancelamento/erro). O binding do Avalonia
    // ignora uma notificação se o valor lido for igual ao último que ele publicou, então a reversão
    // precisa passar por um valor diferente para a caixa voltar ao estado certo.
    private bool _simulationShown;
    private bool _startShown;

    public SettingsViewModel(MainViewModel main, AppHost host)
    {
        _main = main;
        _host = host;
        _simulationShown = host.Config.Settings.SimulationMode;
        _startShown = host.Config.Settings.StartWithSystem;
    }

    private void Show(ref bool shown, bool value, string property)
    {
        shown = !value;
        OnPropertyChanged(property);
        shown = value;
        OnPropertyChanged(property);
    }

    private AppSettings S => _host.Config.Settings;

    /// <summary>O App aplica o tema quando isto dispara.</summary>
    public event Action<ThemeChoice>? ThemeChanged;

    public bool StartWithSystem
    {
        get => _startShown;
        set
        {
            if (S.StartWithSystem == value) return;
            _startShown = value;
            try
            {
                _host.Platform.SetStartWithSystem(value, Environment.ProcessPath ?? "SwapDeskCC");
                S.StartWithSystem = value;
                _host.Save();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine(e);
                Dispatcher.UIThread.Post(() => Show(ref _startShown, S.StartWithSystem, nameof(StartWithSystem)));
            }
        }
    }

    public bool BackupBeforeSwap
    {
        get => S.BackupBeforeSwap;
        set => Set(value, S.BackupBeforeSwap, v => S.BackupBeforeSwap = v);
    }

    public bool NotifyOnSwap
    {
        get => S.NotifyOnSwap;
        set => Set(value, S.NotifyOnSwap, v => S.NotifyOnSwap = v);
    }

    public bool ReopenAfterSwap
    {
        get => S.ReopenAfterSwap;
        set => Set(value, S.ReopenAfterSwap, v => S.ReopenAfterSwap = v);
    }

    public bool SimulationMode
    {
        get => _simulationShown;
        set
        {
            if (_busy || S.SimulationMode == value) return;
            _simulationShown = value;
            if (value)
            {
                _main.DisableRealMode();
                return;
            }
            _ = EnableRealModeAsync();
        }
    }

    private async Task EnableRealModeAsync()
    {
        _busy = true;
        try { await _main.EnableRealModeAsync(); }
        finally
        {
            _busy = false;
            // Volta a caixa se o usuário desistiu ou a importação falhou.
            Show(ref _simulationShown, S.SimulationMode, nameof(SimulationMode));
        }
    }

    public bool IsThemeSystem
    {
        get => S.Theme == ThemeChoice.System;
        set { if (value) SetTheme(ThemeChoice.System); }
    }

    public bool IsThemeLight
    {
        get => S.Theme == ThemeChoice.Light;
        set { if (value) SetTheme(ThemeChoice.Light); }
    }

    public bool IsThemeDark
    {
        get => S.Theme == ThemeChoice.Dark;
        set { if (value) SetTheme(ThemeChoice.Dark); }
    }

    private void SetTheme(ThemeChoice theme)
    {
        if (S.Theme == theme) return;
        S.Theme = theme;
        _host.Save();
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
        ThemeChanged?.Invoke(theme);
    }

    private void Set(bool value, bool current, Action<bool> apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (value == current) return;
        apply(value);
        _host.Save();
        OnPropertyChanged(name);
    }

    // ---------- Informações ----------

    public bool ClaudeFound => _host.Install is not null;
    public string ClaudeStatus => ClaudeFound ? "OK" : "não encontrado";

    public string DetectedPath => _host.Install is { } i
        ? $"{PathDisplay.Shorten(i.ExecutablePath) } · {i.Description}"
        : _host.Platform.Kind == PlatformKind.Linux
            ? "Não há versão oficial para Linux. Instale um build da comunidade (claude-desktop) ou use só o Code."
            : "Instale o Claude Desktop pela Microsoft Store ou pelo site da Anthropic.";

    public string DataPath =>
        $"{PathDisplay.Shorten(_host.DataDir)} → {PathDisplay.Shorten(Path.Combine(_host.ProfilesRoot, _host.Config.ActiveId ?? ""))}";

    public string LinkMethod => _host.Platform.LinkMethod;

    public string ConfigPath => PathDisplay.Shorten(_host.Store.Path);

    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public void RefreshInfo() => OnPropertyChanged(nameof(DataPath));

    [RelayCommand]
    private void OpenProfiles() => OpenFolder(_host.ProfilesRoot);

    [RelayCommand]
    private void OpenBackups() => OpenFolder(_host.BackupsRoot);

    private void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        var opener = _host.Platform.Kind == PlatformKind.Windows ? "explorer.exe" : "xdg-open";
        try { Process.Start(new ProcessStartInfo(opener, $"\"{path}\"") { UseShellExecute = false })?.Dispose(); }
        catch (System.ComponentModel.Win32Exception e) { Debug.WriteLine(e); }
    }
}

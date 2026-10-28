using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using SwapDeskCC.App.Services;
using SwapDeskCC.App.ViewModels;
using SwapDeskCC.App.Views;
using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;

namespace SwapDeskCC.App;

public partial class App : Application
{
    public static bool StartInTray { get; set; }
    public static SingleInstance? Instance { get; set; }

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _window;
    private TrayService? _tray;
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var host = new AppHost();
            var vm = new MainViewModel(host);

            ApplyTheme(host.Config.Settings.Theme);
            vm.Settings.ThemeChanged += ApplyTheme;

            _window = new MainWindow { DataContext = vm };
            _window.Closing += (_, e) =>
            {
                // O ✕ só esconde: o app continua na bandeja.
                if (_quitting) return;
                e.Cancel = true;
                _window.Hide();
            };
            vm.IsWindowVisible = () => _window.IsVisible;

            var toast = new ToastWindow(topCenter: host.Platform.Kind == PlatformKind.Linux);
            toast.Clicked += ShowMainWindow;
            vm.ToastRequested += toast.ShowToast;

            _tray = new TrayService(this, vm, ShowMainWindow, Quit);
            Instance?.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));

            if (!StartInTray) _window.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void Quit()
    {
        _quitting = true;
        _tray?.Dispose();
        _desktop?.Shutdown();
    }

    private void ApplyTheme(ThemeChoice theme) => RequestedThemeVariant = theme switch
    {
        ThemeChoice.Light => ThemeVariant.Light,
        ThemeChoice.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}

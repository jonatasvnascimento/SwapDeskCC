using Avalonia;
using SwapDeskCC.App.Services;

namespace SwapDeskCC.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            instance.SignalFirstInstance();
            return;
        }

        App.Instance = instance;
        App.StartInTray = args.Contains("--tray");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Usado também pelo designer do Avalonia.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

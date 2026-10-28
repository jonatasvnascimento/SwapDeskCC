using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;
using SwapDeskCC.Core.Services;
using SwapDeskCC.Core.Simulation;

namespace SwapDeskCC.App.Services;

/// <summary>Monta os serviços do Core e cria o motor de troca (real ou simulado) conforme as configurações.</summary>
public sealed class AppHost
{
    public IPlatformService Platform { get; }
    public ConfigStore Store { get; }
    public AppConfig Config { get; }
    public ClaudeInstall? Install { get; }
    public string ProfilesRoot { get; }
    public string BackupsRoot { get; }

    public AppHost()
    {
        Platform = PlatformFactory.Create();
        Store = new ConfigStore(Platform.ConfigPath);
        Config = Store.Load();
        Install = Platform.LocateClaude();
        ProfilesRoot = Path.Combine(Platform.AppDataRoot, "profiles");
        BackupsRoot = Path.Combine(Platform.AppDataRoot, "backups");
    }

    public void Save() => Store.Save(Config);

    /// <summary>Pasta de dados do Claude (a detectada, ou a padrão da plataforma se o Claude não foi encontrado).</summary>
    public string DataDir => Install?.DataDir
        ?? (Platform.Kind == PlatformKind.Windows
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claude-Data")
            : Path.Combine(Platform.HomeDir, ".config", "Claude"));

    public ProfileStore Profiles => new(Platform, DataDir, ProfilesRoot);

    public SwapEngine CreateEngine()
    {
        ISwapOperations ops;
        if (Config.Settings.SimulationMode || Install is null)
        {
            var launch = Install?.LaunchId is { } id ? $@"shell:AppsFolder\{id}" : PathDisplay.Shorten(Install?.ExecutablePath);
            ops = new SimulatedSwapOperations(DataDir, ProfilesRoot, BackupsRoot, Platform.LinkMethod,
                Platform.ProcessFilterDescription, launch, Platform.HomeDir,
                Install is null ? null : () => Platform.CountDesktopProcesses(Install));
        }
        else
        {
            ops = new RealSwapOperations(Platform, Install, Profiles, new BackupService(BackupsRoot));
        }
        return new SwapEngine(ops, Config.Settings);
    }

    /// <summary>Pasta do Claude Code para uma conta nova.</summary>
    public string NewCodeConfigDir(string accountId) => Path.Combine(Platform.HomeDir, $".claude-{accountId}");
}

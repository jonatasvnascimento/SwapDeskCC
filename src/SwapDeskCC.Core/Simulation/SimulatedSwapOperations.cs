using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.Core.Simulation;

/// <summary>Modo simulação: mesmas etapas e textos da troca real, só que nenhum arquivo ou processo é tocado.</summary>
public sealed class SimulatedSwapOperations(
    string dataDir,
    string profilesRoot,
    string backupsRoot,
    string linkMethod,
    string processFilter,
    string? launchDisplay,
    string homeDir,
    Func<int>? countDesktopProcesses = null,
    double speed = 1.0) : ISwapOperations
{
    public bool IsSimulation => true;
    public string DataDirDisplay => PathDisplay.Shorten(dataDir);
    public string ProfilesDisplay => PathDisplay.Shorten(profilesRoot);
    public string BackupsDisplay => PathDisplay.Shorten(backupsRoot);
    public string LinkMethod => linkMethod;
    public string ProcessFilterDescription => processFilter;
    public string? LaunchDisplay => launchDisplay;

    public string CodeDirDisplay(Account account) =>
        PathDisplay.Shorten(string.IsNullOrEmpty(account.CodeConfigDir) ? Path.Combine(homeDir, ".claude") : account.CodeConfigDir);

    private Task Wait(int ms, CancellationToken ct) => Task.Delay(TimeSpan.FromMilliseconds(ms * speed), ct);

    public async Task<int> CloseDesktopAsync(CancellationToken ct)
    {
        await Wait(900, ct);
        return countDesktopProcesses?.Invoke() ?? 0; // só conta, não fecha
    }

    public Task WaitForUnlockAsync(CancellationToken ct) => Wait(700, ct);

    public async Task<string> BackupAsync(Account current, CancellationToken ct)
    {
        await Wait(600, ct);
        return PathDisplay.Shorten(Path.Combine(backupsRoot, $"{current.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.zip"));
    }

    public Task ImportCurrentDesktopAsync(Account current, CancellationToken ct) => Wait(700, ct);
    public Task ActivateDesktopAsync(Account target, CancellationToken ct) => Wait(700, ct);
    public Task ActivateCodeAsync(Account target, CancellationToken ct) => Wait(600, ct);
    public Task LaunchDesktopAsync(CancellationToken ct) => Wait(900, ct);
}

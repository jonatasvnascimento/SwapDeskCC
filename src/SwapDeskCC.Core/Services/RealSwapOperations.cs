using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;

namespace SwapDeskCC.Core.Services;

public sealed class RealSwapOperations(IPlatformService platform, ClaudeInstall install, ProfileStore profiles, BackupService backups)
    : ISwapOperations
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan UnlockTimeout = TimeSpan.FromSeconds(10);

    public bool IsSimulation => false;
    public string DataDirDisplay => PathDisplay.Shorten(install.DataDir);
    public string ProfilesDisplay => PathDisplay.Shorten(profiles.ProfilesRoot);
    public string BackupsDisplay => PathDisplay.Shorten(backups.BackupsRoot);
    public string LinkMethod => platform.LinkMethod;
    public string ProcessFilterDescription => platform.ProcessFilterDescription;
    public string? LaunchDisplay => install.LaunchId is { } id ? $@"shell:AppsFolder\{id}" : PathDisplay.Shorten(install.ExecutablePath);

    public string CodeDirDisplay(Account account) => PathDisplay.Shorten(ResolveCodeDir(account));

    private string ResolveCodeDir(Account account) =>
        string.IsNullOrEmpty(account.CodeConfigDir) ? Path.Combine(platform.HomeDir, ".claude") : account.CodeConfigDir;

    public Task<int> CloseDesktopAsync(CancellationToken ct) => platform.CloseDesktopAsync(install, CloseTimeout, ct);

    /// <summary>O Electron segura o "lockfile" enquanto roda; espera até conseguir abri-lo com exclusividade.</summary>
    public async Task WaitForUnlockAsync(CancellationToken ct)
    {
        string[] probes = ["lockfile", "Local State"];
        var deadline = DateTime.UtcNow + UnlockTimeout;
        while (true)
        {
            var locked = probes.Select(p => Path.Combine(install.DataDir, p)).FirstOrDefault(IsLocked);
            if (locked is null) return;
            if (DateTime.UtcNow > deadline) throw new IOException($"Arquivo ainda em uso: {locked}");
            await Task.Delay(300, ct);
        }
    }

    private static bool IsLocked(string file)
    {
        if (!File.Exists(file)) return false;
        try
        {
            using var _ = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public async Task<string> BackupAsync(Account current, CancellationToken ct)
    {
        if (!Directory.Exists(install.DataDir)) return "nada para copiar";
        var info = await backups.CreateAsync(install.DataDir, current.Id, ct);
        var skipped = info.Skipped > 0 ? $" ({info.Skipped} arquivo(s) em uso ignorado(s))" : "";
        return PathDisplay.Shorten(info.Path) + skipped;
    }

    public Task ImportCurrentDesktopAsync(Account current, CancellationToken ct) =>
        Task.Run(() => profiles.ImportCurrent(current), ct);

    public Task ActivateDesktopAsync(Account target, CancellationToken ct) =>
        Task.Run(() => profiles.Activate(target), ct);

    public Task ActivateCodeAsync(Account target, CancellationToken ct) => Task.Run(() =>
    {
        if (!string.IsNullOrEmpty(target.CodeConfigDir)) Directory.CreateDirectory(target.CodeConfigDir);
        platform.SetCodeConfigDir(target.CodeConfigDir);
    }, ct);

    public Task LaunchDesktopAsync(CancellationToken ct)
    {
        platform.LaunchDesktop(install);
        return Task.CompletedTask;
    }
}

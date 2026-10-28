using System.Diagnostics;

namespace SwapDeskCC.Core.Platform;

public abstract class PlatformServiceBase : IPlatformService
{
    public abstract PlatformKind Kind { get; }
    public abstract string LinkMethod { get; }
    public abstract bool DesktopOfficial { get; }
    public abstract string AppDataRoot { get; }
    public abstract string ConfigPath { get; }
    public abstract string ProcessFilterDescription { get; }

    public string HomeDir { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public virtual bool IsRunningInsidePackage => false;

    public abstract ClaudeInstall? LocateClaude();
    public abstract void LaunchDesktop(ClaudeInstall install);
    public abstract void CreateDirectoryLink(string link, string target);
    public abstract void SetCodeConfigDir(string? dir);
    public abstract string? GetCodeConfigDir();
    public abstract void SetStartWithSystem(bool enabled, string executablePath);

    protected abstract IEnumerable<Process> FindDesktopProcesses(ClaudeInstall install);

    /// <summary>Pedido educado para fechar (CloseMainWindow / SIGTERM).</summary>
    protected abstract void RequestClose(Process process);

    public int CountDesktopProcesses(ClaudeInstall install)
    {
        var list = FindDesktopProcesses(install).ToList();
        foreach (var p in list) p.Dispose();
        return list.Count;
    }

    public async Task<int> CloseDesktopAsync(ClaudeInstall install, TimeSpan timeout, CancellationToken ct)
    {
        var processes = FindDesktopProcesses(install).ToList();
        if (processes.Count == 0) return 0;

        try
        {
            foreach (var p in processes) TryRun(() => RequestClose(p));
            if (await WaitAllExitedAsync(processes, TimeSpan.FromSeconds(3), ct)) return processes.Count;

            // Ainda aberto (ex.: minimizado na bandeja): força o encerramento.
            foreach (var p in processes.Where(p => !HasExited(p))) TryRun(() => p.Kill(entireProcessTree: true));
            if (await WaitAllExitedAsync(processes, timeout, ct)) return processes.Count;

            throw new InvalidOperationException("O Claude Desktop não fechou a tempo. Feche-o manualmente e tente de novo.");
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    public string? GetLinkTarget(string path)
    {
        var info = new DirectoryInfo(path);
        return info.LinkTarget is { } target ? Path.GetFullPath(target, Path.GetDirectoryName(path)!) : null;
    }

    public void RemoveDirectoryLink(string link)
    {
        if (GetLinkTarget(link) is null)
            throw new InvalidOperationException($"Recusado: \"{link}\" não é um link, e o SwapDeskCC nunca apaga pastas reais.");
        DeleteLink(link);
    }

    protected abstract void DeleteLink(string link);

    private static async Task<bool> WaitAllExitedAsync(List<Process> processes, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (processes.All(HasExited)) return true;
            await Task.Delay(200, ct);
        }
        return processes.All(HasExited);
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; }
        catch (InvalidOperationException) { return true; }
    }

    protected static void TryRun(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
    }

    protected static string? TryGetPath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { return null; }
    }
}

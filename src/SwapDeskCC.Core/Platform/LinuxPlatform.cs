using System.Diagnostics;
using System.Runtime.Versioning;

namespace SwapDeskCC.Core.Platform;

/// <summary>
/// Linux: o Claude Desktop não tem versão oficial, então o suporte ao Desktop é experimental
/// (builds da comunidade como claude-desktop). O Claude Code é oficial e funciona normalmente.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxPlatform : PlatformServiceBase
{
    private const string Marker = "# SwapDeskCC";

    private static readonly string[] ExecutableNames = ["claude-desktop", "Claude"];
    private static readonly string[] KnownDirs = ["/usr/bin", "/usr/local/bin", "/opt/Claude", "/usr/lib/claude-desktop"];

    public override PlatformKind Kind => PlatformKind.Linux;
    public override string LinkMethod => "symlink";
    public override bool DesktopOfficial => false;
    public override string ProcessFilterDescription => "só processos do claude-desktop (o Claude Code não é afetado)";

    private string XdgConfig => Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } x ? x : Path.Combine(HomeDir, ".config");
    private string XdgData => Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(HomeDir, ".local", "share");

    public override string AppDataRoot => Path.Combine(XdgData, "swapdeskcc");
    public override string ConfigPath => Path.Combine(XdgConfig, "swapdeskcc", "config.json");

    public override ClaudeInstall? LocateClaude()
    {
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries);
        var exe = pathDirs.Concat(KnownDirs)
            .SelectMany(d => ExecutableNames.Select(n => Path.Combine(d, n)))
            .FirstOrDefault(File.Exists);
        var dataDir = Path.Combine(XdgConfig, "Claude");
        if (exe is null && !Directory.Exists(dataDir)) return null;
        return new ClaudeInstall(exe, dataDir, null, exe is null ? "Build não oficial (executável não encontrado)" : "Build não oficial");
    }

    protected override IEnumerable<Process> FindDesktopProcesses(ClaudeInstall install)
    {
        var installDir = install.ExecutablePath is { } exe ? Path.GetDirectoryName(exe) : null;
        foreach (var p in Process.GetProcesses())
        {
            var cmdline = ReadCmdline(p.Id);
            var path = TryGetPath(p);
            var isDesktop = cmdline.Contains("claude-desktop", StringComparison.OrdinalIgnoreCase)
                || (installDir is not null && installDir != "/usr/bin" && path?.StartsWith(installDir) == true);
            if (isDesktop) yield return p;
            else p.Dispose();
        }
    }

    private static string ReadCmdline(int pid)
    {
        try { return File.ReadAllText($"/proc/{pid}/cmdline").Replace('\0', ' '); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    protected override void RequestClose(Process process) =>
        Process.Start(new ProcessStartInfo("kill", ["-TERM", process.Id.ToString()]) { UseShellExecute = false })?.WaitForExit();

    public override void LaunchDesktop(ClaudeInstall install)
    {
        var exe = install.ExecutablePath ?? throw new InvalidOperationException("Executável do Claude Desktop não encontrado.");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })?.Dispose();
    }

    public override void CreateDirectoryLink(string link, string target) => Directory.CreateSymbolicLink(link, target);

    // File.Delete faz unlink no symlink, sem seguir para o destino.
    protected override void DeleteLink(string link) => File.Delete(link);

    /// <summary>
    /// Grava a variável em environment.d (sessão gráfica) e num env.sh carregado pelo ~/.profile e ~/.bashrc.
    /// </summary>
    public override void SetCodeConfigDir(string? dir)
    {
        var envD = Path.Combine(XdgConfig, "environment.d", "swapdeskcc.conf");
        var envSh = Path.Combine(XdgConfig, "swapdeskcc", "env.sh");
        Directory.CreateDirectory(Path.GetDirectoryName(envD)!);
        Directory.CreateDirectory(Path.GetDirectoryName(envSh)!);

        if (string.IsNullOrEmpty(dir))
        {
            File.Delete(envD);
            File.WriteAllText(envSh, "unset CLAUDE_CONFIG_DIR\n");
        }
        else
        {
            File.WriteAllText(envD, $"CLAUDE_CONFIG_DIR={dir}\n");
            File.WriteAllText(envSh, $"export CLAUDE_CONFIG_DIR=\"{dir}\"\n");
        }

        var sourceLine = $"[ -f \"{envSh}\" ] && . \"{envSh}\" {Marker}";
        foreach (var rc in new[] { ".profile", ".bashrc" }.Select(f => Path.Combine(HomeDir, f)))
        {
            if (File.Exists(rc) && File.ReadAllText(rc).Contains(Marker)) continue;
            File.AppendAllText(rc, $"\n{sourceLine}\n");
        }
    }

    public override string? GetCodeConfigDir()
    {
        var envD = Path.Combine(XdgConfig, "environment.d", "swapdeskcc.conf");
        if (!File.Exists(envD)) return null;
        var line = File.ReadAllLines(envD).FirstOrDefault(l => l.StartsWith("CLAUDE_CONFIG_DIR="));
        return line?["CLAUDE_CONFIG_DIR=".Length..];
    }

    public override void SetStartWithSystem(bool enabled, string executablePath)
    {
        var file = Path.Combine(XdgConfig, "autostart", "swapdeskcc.desktop");
        if (!enabled)
        {
            File.Delete(file);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, $"""
            [Desktop Entry]
            Type=Application
            Name=SwapDeskCC
            Exec="{executablePath}" --tray
            X-GNOME-Autostart-enabled=true

            """);
    }
}

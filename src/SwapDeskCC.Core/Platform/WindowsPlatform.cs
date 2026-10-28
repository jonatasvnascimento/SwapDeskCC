using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SwapDeskCC.Core.Platform;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatform : PlatformServiceBase
{
    private const string PackagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string CodeVar = "CLAUDE_CONFIG_DIR";

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public override PlatformKind Kind => PlatformKind.Windows;
    public override string LinkMethod => "junction";
    public override bool DesktopOfficial => true;
    public override string AppDataRoot => Path.Combine(LocalAppData, "SwapDeskCC");
    public override string ConfigPath => Path.Combine(RoamingAppData, "SwapDeskCC", "config.json");
    public override string ProcessFilterDescription => @"só processos em WindowsApps\Claude_* (o Claude Code não é afetado)";

    private readonly Lazy<bool> _insidePackage = new(DetectPackageContainer);

    public override bool IsRunningInsidePackage => _insidePackage.Value;

    /// <summary>
    /// Processos abertos a partir de um app MSIX (ex.: terminal do Claude Desktop) herdam o redirecionamento
    /// de AppData mesmo sem identidade de pacote. Detecta das duas formas: pela API e por uma pasta de prova.
    /// </summary>
    private static bool DetectPackageContainer()
    {
        var length = 0u;
        // APPMODEL_ERROR_NO_PACKAGE (15700) = processo sem identidade de pacote.
        if (GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage) return true;

        var probeName = $".swapdeskcc-probe-{Guid.NewGuid():N}";
        var probe = Path.Combine(RoamingAppData, probeName);
        try
        {
            Directory.CreateDirectory(probe);
            var packages = Path.Combine(LocalAppData, "Packages");
            return Directory.Exists(packages) && Directory.EnumerateDirectories(packages)
                .Any(p => Directory.Exists(Path.Combine(p, "LocalCache", "Roaming", probeName)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { Directory.Delete(probe); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private const int AppModelErrorNoPackage = 15700;

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);

    public override ClaudeInstall? LocateClaude() => LocateMsix() ?? LocateSquirrel();

    /// <summary>Instalação MSIX: o registro do usuário lista os pacotes e a pasta de cada um.</summary>
    private static ClaudeInstall? LocateMsix()
    {
        using var packages = Registry.CurrentUser.OpenSubKey(PackagesKey);
        if (packages is null) return null;

        // Ex.: Claude_4.29.0.0_x64__pzs8sxrjxfjjc — fica com a maior versão.
        var fullName = packages.GetSubKeyNames()
            .Where(n => n.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase) && n.Contains("__"))
            .OrderByDescending(n => Version.TryParse(n.Split('_')[1], out var v) ? v : new Version())
            .FirstOrDefault();
        if (fullName is null) return null;

        using var key = packages.OpenSubKey(fullName);
        var root = key?.GetValue("PackageRootFolder") as string;
        if (root is null) return null;

        var publisherId = fullName[(fullName.LastIndexOf("__", StringComparison.Ordinal) + 2)..];
        var family = $"Claude_{publisherId}";
        var appId = ReadAppId(Path.Combine(root, "AppxManifest.xml")) ?? "Claude";
        var exe = Path.Combine(root, "app", "Claude.exe");

        string[] candidates =
        [
            Path.Combine(LocalAppData, "Claude-Data", $"claude_{publisherId}"),
            Path.Combine(LocalAppData, "Packages", family, "LocalCache", "Roaming", "Claude"),
            Path.Combine(RoamingAppData, "Claude"),
        ];
        var dataDir = candidates.FirstOrDefault(LooksLikeDataDir) ?? candidates[0];

        return new ClaudeInstall(exe, dataDir, $"{family}!{appId}", $"MSIX {fullName.Split('_')[1]}");
    }

    /// <summary>Instalação antiga (Squirrel) em %LOCALAPPDATA%\AnthropicClaude.</summary>
    private static ClaudeInstall? LocateSquirrel()
    {
        var exe = Path.Combine(LocalAppData, "AnthropicClaude", "claude.exe");
        if (!File.Exists(exe)) return null;
        return new ClaudeInstall(exe, Path.Combine(RoamingAppData, "Claude"), null, "Instalação clássica");
    }

    private static bool LooksLikeDataDir(string dir) =>
        new DirectoryInfo(dir).LinkTarget is not null
        || File.Exists(Path.Combine(dir, "Local State"))
        || File.Exists(Path.Combine(dir, "config.json"));

    private static string? ReadAppId(string manifest)
    {
        try
        {
            var m = AppIdRegex().Match(File.ReadAllText(manifest));
            return m.Success ? m.Groups[1].Value : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    [GeneratedRegex("<Application\\s+Id=\"([^\"]+)\"")]
    private static partial Regex AppIdRegex();

    protected override IEnumerable<Process> FindDesktopProcesses(ClaudeInstall install)
    {
        var installDir = install.ExecutablePath is { } exe ? Path.GetDirectoryName(exe) : null;
        foreach (var p in Process.GetProcessesByName("claude"))
        {
            var path = TryGetPath(p);
            var isDesktop = path is not null && (
                (installDir is not null && path.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                || path.Contains(@"\WindowsApps\Claude_", StringComparison.OrdinalIgnoreCase));
            if (isDesktop) yield return p;
            else p.Dispose();
        }
    }

    protected override void RequestClose(Process process) => process.CloseMainWindow();

    public override void LaunchDesktop(ClaudeInstall install)
    {
        var psi = install.LaunchId is { } aumid
            ? new ProcessStartInfo("explorer.exe", $@"shell:AppsFolder\{aumid}") { UseShellExecute = true }
            : new ProcessStartInfo(install.ExecutablePath ?? throw new InvalidOperationException("Claude Desktop não encontrado.")) { UseShellExecute = true };
        Process.Start(psi)?.Dispose();
    }

    /// <summary>Junction não exige admin nem modo desenvolvedor (ao contrário do symlink).</summary>
    public override void CreateDirectoryLink(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", link, target },
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível executar o mklink.");
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException($"Falha ao criar junction: {error.Trim()}");
    }

    // Em junction, Directory.Delete sem recursão remove só o ponto de montagem.
    protected override void DeleteLink(string link) => Directory.Delete(link, recursive: false);

    public override void SetCodeConfigDir(string? dir) =>
        Environment.SetEnvironmentVariable(CodeVar, string.IsNullOrEmpty(dir) ? null : dir, EnvironmentVariableTarget.User);

    public override string? GetCodeConfigDir() =>
        Environment.GetEnvironmentVariable(CodeVar, EnvironmentVariableTarget.User);

    public override void SetStartWithSystem(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("SwapDeskCC", $"\"{executablePath}\" --tray");
        else key.DeleteValue("SwapDeskCC", throwOnMissingValue: false);
    }
}

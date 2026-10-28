namespace SwapDeskCC.Core.Platform;

public enum PlatformKind
{
    Windows,
    Linux,
    Other,
}

/// <param name="ExecutablePath">Claude.exe / claude-desktop, se encontrado.</param>
/// <param name="DataDir">Pasta de dados do Claude Desktop (a que vira link para o perfil ativo).</param>
/// <param name="LaunchId">AUMID do pacote MSIX (Windows). Null = abrir pelo executável.</param>
/// <param name="Description">Texto curto para a tela de configurações.</param>
public sealed record ClaudeInstall(string? ExecutablePath, string DataDir, string? LaunchId, string Description);

/// <summary>Tudo o que depende do sistema operacional fica atrás desta interface.</summary>
public interface IPlatformService
{
    PlatformKind Kind { get; }

    /// <summary>"junction" no Windows, "symlink" no Linux.</summary>
    string LinkMethod { get; }

    /// <summary>False quando o Claude Desktop não tem versão oficial na plataforma (Linux).</summary>
    bool DesktopOfficial { get; }

    /// <summary>Raiz dos perfis e backups (no mesmo volume da pasta do Claude).</summary>
    string AppDataRoot { get; }

    string ConfigPath { get; }

    string HomeDir { get; }

    /// <summary>Texto que explica quais processos são fechados na troca.</summary>
    string ProcessFilterDescription { get; }

    /// <summary>
    /// True quando o processo roda dentro do contêiner de um pacote MSIX (ex.: aberto por um terminal
    /// do próprio Claude Desktop). Ali as gravações em AppData são redirecionadas, então a troca real é recusada.
    /// </summary>
    bool IsRunningInsidePackage { get; }

    ClaudeInstall? LocateClaude();

    /// <summary>Conta os processos do Claude Desktop (sem fechar nada).</summary>
    int CountDesktopProcesses(ClaudeInstall install);

    /// <summary>Fecha só o Claude Desktop. Lança exceção se ele não fechar dentro do tempo.</summary>
    Task<int> CloseDesktopAsync(ClaudeInstall install, TimeSpan timeout, CancellationToken ct);

    void LaunchDesktop(ClaudeInstall install);

    void CreateDirectoryLink(string link, string target);

    /// <summary>Destino do link, ou null se o caminho não for um link.</summary>
    string? GetLinkTarget(string path);

    /// <summary>Remove apenas o link (nunca o conteúdo). Lança exceção se o caminho não for link.</summary>
    void RemoveDirectoryLink(string link);

    /// <summary>Define CLAUDE_CONFIG_DIR para novos terminais. Null/vazio remove a variável.</summary>
    void SetCodeConfigDir(string? dir);

    string? GetCodeConfigDir();

    void SetStartWithSystem(bool enabled, string executablePath);
}

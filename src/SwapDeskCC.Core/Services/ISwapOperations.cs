using SwapDeskCC.Core.Models;

namespace SwapDeskCC.Core.Services;

/// <summary>
/// As ações de baixo nível da troca. Existem duas implementações:
/// <see cref="RealSwapOperations"/> (mexe nos arquivos) e <see cref="SimulatedSwapOperations"/> (só anima).
/// </summary>
public interface ISwapOperations
{
    bool IsSimulation { get; }

    /// <summary>Caminho da pasta de dados do Claude, para exibir nas etapas.</summary>
    string DataDirDisplay { get; }
    string ProfilesDisplay { get; }
    string BackupsDisplay { get; }
    string LinkMethod { get; }
    string ProcessFilterDescription { get; }
    string? LaunchDisplay { get; }

    /// <summary>Pasta do Claude Code que a conta usa (para exibir).</summary>
    string CodeDirDisplay(Account account);

    Task<int> CloseDesktopAsync(CancellationToken ct);
    Task WaitForUnlockAsync(CancellationToken ct);
    Task<string> BackupAsync(Account current, CancellationToken ct);
    Task ImportCurrentDesktopAsync(Account current, CancellationToken ct);
    Task ActivateDesktopAsync(Account target, CancellationToken ct);
    Task ActivateCodeAsync(Account target, CancellationToken ct);
    Task LaunchDesktopAsync(CancellationToken ct);
}

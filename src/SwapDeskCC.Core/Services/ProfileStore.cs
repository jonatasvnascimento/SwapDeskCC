using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;

namespace SwapDeskCC.Core.Services;

/// <summary>
/// Cada conta tem uma pasta de perfil. A pasta de dados do Claude vira um link (junction/symlink)
/// para o perfil da conta ativa, então trocar de conta é só repontar o link.
/// </summary>
public sealed class ProfileStore(IPlatformService platform, string dataDir, string profilesRoot)
{
    public string DataDir { get; } = dataDir;
    public string ProfilesRoot { get; } = profilesRoot;

    public string ProfileDir(Account account) => Path.Combine(ProfilesRoot, account.Id, "desktop");

    /// <summary>True quando a pasta de dados já é um link gerenciado pelo SwapDeskCC.</summary>
    public bool IsImported => platform.GetLinkTarget(DataDir) is not null;

    public string? ActiveProfileDir => platform.GetLinkTarget(DataDir);

    /// <summary>
    /// Primeira vez: move a pasta real do Claude para o perfil da conta e cria o link no lugar.
    /// Se o link falhar, a pasta volta para onde estava.
    /// </summary>
    public void ImportCurrent(Account account)
    {
        if (IsImported) throw new InvalidOperationException("A pasta do Claude já está sob controle do SwapDeskCC.");

        var profile = ProfileDir(account);
        if (Directory.Exists(profile) && Directory.EnumerateFileSystemEntries(profile).Any())
            throw new InvalidOperationException($"O perfil \"{profile}\" já existe e não está vazio.");

        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        if (Directory.Exists(profile)) Directory.Delete(profile); // vazio

        if (Directory.Exists(DataDir)) Directory.Move(DataDir, profile);
        else Directory.CreateDirectory(profile);

        try
        {
            platform.CreateDirectoryLink(DataDir, profile);
        }
        catch
        {
            if (!Directory.Exists(DataDir)) Directory.Move(profile, DataDir);
            throw;
        }
    }

    /// <summary>Aponta a pasta de dados para o perfil da conta. Perfil novo começa vazio (Claude abre na tela de login).</summary>
    public void Activate(Account account)
    {
        var previous = platform.GetLinkTarget(DataDir);
        if (previous is null && Directory.Exists(DataDir))
            throw new InvalidOperationException("A pasta do Claude ainda não foi importada. Desligue o modo simulação pelas Configurações.");

        var target = ProfileDir(account);
        Directory.CreateDirectory(target);
        if (previous is not null && PathsEqual(previous, target)) return;

        if (previous is not null) platform.RemoveDirectoryLink(DataDir);
        try
        {
            platform.CreateDirectoryLink(DataDir, target);
        }
        catch
        {
            if (previous is not null && !Directory.Exists(DataDir)) platform.CreateDirectoryLink(DataDir, previous);
            throw;
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

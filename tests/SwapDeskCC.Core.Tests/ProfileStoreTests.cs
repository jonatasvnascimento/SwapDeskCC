using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Platform;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.Core.Tests;

/// <summary>Usa junction (Windows) ou symlink (Linux) de verdade, numa pasta temporária.</summary>
public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("swapdeskcc-prof-").FullName;
    private readonly IPlatformService _platform = PlatformFactory.Create();
    private readonly string _dataDir;
    private readonly ProfileStore _store;

    private static readonly Account A = new() { Id = "a", Name = "A" };
    private static readonly Account B = new() { Id = "b", Name = "B" };

    public ProfileStoreTests()
    {
        _dataDir = Path.Combine(_root, "Claude-Data");
        _store = new ProfileStore(_platform, _dataDir, Path.Combine(_root, "profiles"));
    }

    public void Dispose()
    {
        if (_platform.GetLinkTarget(_dataDir) is not null) _platform.RemoveDirectoryLink(_dataDir);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Import_moves_real_folder_into_profile_and_links_it()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(Path.Combine(_dataDir, "Local State"), "conta A");

        _store.ImportCurrent(A);

        Assert.True(_store.IsImported);
        Assert.Equal("conta A", File.ReadAllText(Path.Combine(_store.ProfileDir(A), "Local State")));
        Assert.Equal("conta A", File.ReadAllText(Path.Combine(_dataDir, "Local State"))); // pelo link
    }

    [Fact]
    public void Activate_switches_between_profiles_without_losing_data()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(Path.Combine(_dataDir, "Local State"), "conta A");
        _store.ImportCurrent(A);

        _store.Activate(B);
        Assert.False(File.Exists(Path.Combine(_dataDir, "Local State"))); // perfil novo = vazio (tela de login)
        File.WriteAllText(Path.Combine(_dataDir, "Local State"), "conta B");

        _store.Activate(A);
        Assert.Equal("conta A", File.ReadAllText(Path.Combine(_dataDir, "Local State")));

        _store.Activate(B);
        Assert.Equal("conta B", File.ReadAllText(Path.Combine(_dataDir, "Local State")));
    }

    [Fact]
    public void Activate_refuses_when_real_folder_was_not_imported()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(Path.Combine(_dataDir, "Local State"), "real");

        Assert.Throws<InvalidOperationException>(() => _store.Activate(B));
        Assert.Equal("real", File.ReadAllText(Path.Combine(_dataDir, "Local State")));
    }

    [Fact]
    public void RemoveDirectoryLink_never_deletes_a_real_folder()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(Path.Combine(_dataDir, "x"), "x");

        Assert.Throws<InvalidOperationException>(() => _platform.RemoveDirectoryLink(_dataDir));
        Assert.True(File.Exists(Path.Combine(_dataDir, "x")));
    }

    [Fact]
    public void Removing_link_keeps_profile_contents()
    {
        Directory.CreateDirectory(_dataDir);
        File.WriteAllText(Path.Combine(_dataDir, "Local State"), "conta A");
        _store.ImportCurrent(A);

        _platform.RemoveDirectoryLink(_dataDir);

        Assert.False(Directory.Exists(_dataDir));
        Assert.True(File.Exists(Path.Combine(_store.ProfileDir(A), "Local State")));
    }
}

using System.IO.Compression;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.Core.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("swapdeskcc-bak-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Zips_profile_but_skips_caches()
    {
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(src, "Local Storage"));
        Directory.CreateDirectory(Path.Combine(src, "Cache"));
        File.WriteAllText(Path.Combine(src, "config.json"), "{}");
        File.WriteAllText(Path.Combine(src, "Local Storage", "leveldb"), "login");
        File.WriteAllText(Path.Combine(src, "Cache", "data_0"), "lixo");

        var info = await new BackupService(Path.Combine(_root, "backups")).CreateAsync(src, "pessoal");

        using var zip = ZipFile.OpenRead(info.Path);
        var names = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["Local Storage/leveldb", "config.json"], names);
    }

    [Fact]
    public async Task Keeps_only_latest_backups_per_account()
    {
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a"), "a");
        var backups = Path.Combine(_root, "backups");
        Directory.CreateDirectory(backups);
        for (var i = 1; i <= 4; i++) File.WriteAllText(Path.Combine(backups, $"pessoal-2020010{i}-000000.zip"), "");
        File.WriteAllText(Path.Combine(backups, "trabalho-20200101-000000.zip"), "");

        await new BackupService(backups, keepPerLabel: 3).CreateAsync(src, "pessoal");

        Assert.Equal(3, Directory.GetFiles(backups, "pessoal-*.zip").Length);
        Assert.Single(Directory.GetFiles(backups, "trabalho-*.zip"));
    }
}

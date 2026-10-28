using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("swapdeskcc-cfg-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_returns_default_with_current_account()
    {
        var config = new ConfigStore(Path.Combine(_dir, "config.json")).Load();

        Assert.Single(config.Accounts);
        Assert.Equal("atual", config.ActiveId);
        Assert.True(config.Settings.SimulationMode);
    }

    [Fact]
    public void Round_trips_accounts_scope_and_settings()
    {
        var store = new ConfigStore(Path.Combine(_dir, "sub", "config.json"));
        var config = AppConfig.CreateDefault();
        config.Accounts.Add(new Account { Id = "trabalho", Name = "Trabalho", Color = "#3367d6" });
        config.ActiveId = "trabalho";
        config.Scope = SwapScope.CodeOnly;
        config.Settings.Theme = ThemeChoice.Dark;
        config.AddActivity("Conta atual → Trabalho");
        store.Save(config);

        var loaded = store.Load();

        Assert.Equal(2, loaded.Accounts.Count);
        Assert.Equal("Trabalho", loaded.ActiveAccount?.Name);
        Assert.Equal(SwapScope.CodeOnly, loaded.Scope);
        Assert.Equal(ThemeChoice.Dark, loaded.Settings.Theme);
        Assert.Single(loaded.Activity);
    }

    [Fact]
    public void Corrupt_file_is_kept_aside_and_default_is_returned()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, "{ isso não é json");

        var config = new ConfigStore(path).Load();

        Assert.Equal("atual", config.ActiveId);
        Assert.True(File.Exists(path + ".corrompido"));
    }

    [Fact]
    public void Activity_keeps_only_latest_entries()
    {
        var config = new AppConfig();
        for (var i = 0; i < 10; i++) config.AddActivity($"troca {i}");

        Assert.Equal(AppConfig.MaxActivity, config.Activity.Count);
        Assert.Equal("troca 9", config.Activity[0].Text);
    }

    [Theory]
    [InlineData("Freela", "freela")]
    [InlineData("Conta São João", "contasaojoao")]
    [InlineData("!!!", "")]
    public void Slugify_produces_folder_safe_ids(string name, string expected) =>
        Assert.Equal(expected, Account.Slugify(name));

    [Fact]
    public void New_account_id_avoids_collisions()
    {
        var config = AppConfig.CreateDefault();
        config.Accounts.Add(new Account { Id = "freela" });

        Assert.Equal("freela2", config.NewAccountId("Freela"));
        Assert.Equal("conta", config.NewAccountId("!!!"));
    }
}

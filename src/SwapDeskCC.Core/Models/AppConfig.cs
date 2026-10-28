using System.Text.Json.Serialization;

namespace SwapDeskCC.Core.Models;

public enum SwapScope
{
    Both,
    DesktopOnly,
    CodeOnly,
}

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

public sealed class AppSettings
{
    public bool StartWithSystem { get; set; }
    public bool BackupBeforeSwap { get; set; } = true;
    public bool NotifyOnSwap { get; set; } = true;
    public bool ReopenAfterSwap { get; set; } = true;

    /// <summary>Quando ligado, a troca só é simulada e nenhum arquivo é tocado.</summary>
    public bool SimulationMode { get; set; } = true;

    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
}

public sealed class ActivityEntry
{
    public DateTime At { get; set; }
    public string Text { get; set; } = "";
}

public sealed class AppConfig
{
    public List<Account> Accounts { get; set; } = [];
    public string? ActiveId { get; set; }
    public SwapScope Scope { get; set; } = SwapScope.Both;
    public AppSettings Settings { get; set; } = new();
    public List<ActivityEntry> Activity { get; set; } = [];

    /// <summary>True depois que a pasta real do Claude Desktop foi movida para um perfil e virou link.</summary>
    public bool DesktopImported { get; set; }

    public const int MaxActivity = 6;

    [JsonIgnore]
    public Account? ActiveAccount => Accounts.FirstOrDefault(a => a.Id == ActiveId);

    public void AddActivity(string text)
    {
        Activity.Insert(0, new ActivityEntry { At = DateTime.Now, Text = text });
        if (Activity.Count > MaxActivity) Activity.RemoveRange(MaxActivity, Activity.Count - MaxActivity);
    }

    public string NewAccountId(string name)
    {
        var baseId = Account.Slugify(name);
        if (baseId.Length == 0) baseId = "conta";
        var id = baseId;
        for (var i = 2; Accounts.Any(a => a.Id == id); i++) id = $"{baseId}{i}";
        return id;
    }

    public static AppConfig CreateDefault() => new()
    {
        Accounts = [new Account { Id = "atual", Name = "Conta atual", Color = Account.Palette[0] }],
        ActiveId = "atual",
    };
}

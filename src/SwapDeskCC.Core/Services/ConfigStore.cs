using System.Text.Json;
using System.Text.Json.Serialization;
using SwapDeskCC.Core.Models;

namespace SwapDeskCC.Core.Services;

/// <summary>Lê e grava o config.json do SwapDeskCC (gravação atômica via arquivo temporário).</summary>
public sealed class ConfigStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Path { get; } = path;

    public AppConfig Load()
    {
        if (!File.Exists(Path)) return AppConfig.CreateDefault();
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path), Options);
            if (config is null || config.Accounts.Count == 0) return AppConfig.CreateDefault();
            if (config.ActiveAccount is null) config.ActiveId = config.Accounts[0].Id;
            return config;
        }
        catch (JsonException)
        {
            // Config corrompido: guarda uma cópia para análise e recomeça.
            File.Copy(Path, Path + ".corrompido", overwrite: true);
            return AppConfig.CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, Path, overwrite: true);
    }
}

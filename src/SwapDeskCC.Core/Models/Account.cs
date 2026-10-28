using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SwapDeskCC.Core.Models;

public sealed class Account
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Color { get; set; } = "#c96442";

    /// <summary>
    /// Pasta do Claude Code desta conta. Vazio = pasta padrão (~/.claude), sem CLAUDE_CONFIG_DIR.
    /// </summary>
    public string CodeConfigDir { get; set; } = "";

    [JsonIgnore]
    public string Initials
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var s = string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
            return s.Length > 0 ? s : "?";
        }
    }

    /// <summary>Gera um id curto e seguro para nomes de pasta a partir do apelido.</summary>
    public static string Slugify(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(lower);
        }
        return sb.ToString();
    }

    public static readonly string[] Palette = ["#c96442", "#3367d6", "#2e7d32", "#8e44ad", "#d81b60", "#00897b"];
}

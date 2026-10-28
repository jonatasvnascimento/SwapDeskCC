using System.Globalization;
using System.Text.RegularExpressions;

namespace SwapDeskCC.Core.Models;

/// <summary>
/// Converte cores digitadas pelo usuário (estilo CSS) para o formato guardado no config,
/// que é o do Avalonia: "#RRGGBB" (opaca) ou "#AARRGGBB" (com transparência).
/// </summary>
/// <remarks>
/// Aceita: #RGB, #RRGGBB, #RRGGBBAA (CSS: alfa no fim), rgb(r, g, b), rgba(r, g, b, a)
/// e também "rgb(r g b / a)". O alfa pode ser 0–1 ou porcentagem.
/// </remarks>
public static partial class ColorText
{
    public static bool TryParseInput(string? text, out string stored)
    {
        stored = "";
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();

        if (s.StartsWith('#')) return TryParseCssHex(s[1..], out stored);

        var m = RgbRegex().Match(s);
        if (!m.Success) return false;

        if (!TryChannel(m.Groups["r"].Value, out var r) || !TryChannel(m.Groups["g"].Value, out var g) || !TryChannel(m.Groups["b"].Value, out var b))
            return false;

        var alpha = 1.0;
        if (m.Groups["a"].Success && !TryAlpha(m.Groups["a"].Value, out alpha)) return false;

        stored = Format(r, g, b, (byte)Math.Round(alpha * 255));
        return true;
    }

    /// <summary>Texto "rgba(r, g, b, a)" de uma cor guardada (#RRGGBB ou #AARRGGBB).</summary>
    public static string ToRgba(string stored)
    {
        if (!TryReadStored(stored, out var r, out var g, out var b, out var a)) return "";
        var alpha = Math.Round(a / 255.0, 2).ToString("0.##", CultureInfo.InvariantCulture);
        return $"rgba({r}, {g}, {b}, {alpha})";
    }

    /// <summary>Compara duas cores guardadas ignorando maiúsculas e o alfa opaco implícito.</summary>
    public static bool SameColor(string a, string b) =>
        TryReadStored(a, out var r1, out var g1, out var b1, out var a1)
        && TryReadStored(b, out var r2, out var g2, out var b2, out var a2)
        && r1 == r2 && g1 == g2 && b1 == b2 && a1 == a2;

    private static bool TryReadStored(string stored, out byte r, out byte g, out byte b, out byte a)
    {
        r = g = b = 0;
        a = 255;
        var hex = stored.TrimStart('#');
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        switch (hex.Length)
        {
            case 6:
                (r, g, b) = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
                return true;
            case 8: // Avalonia: AARRGGBB
                (a, r, g, b) = ((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseCssHex(string hex, out string stored)
    {
        stored = "";
        if (hex.Length is 3 or 4) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;

        stored = hex.Length == 6
            ? Format((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255)
            : Format((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v); // CSS: RRGGBBAA
        return true;
    }

    private static bool TryChannel(string text, out byte value)
    {
        value = 0;
        if (text.EndsWith('%'))
        {
            if (!double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) || pct is < 0 or > 100) return false;
            value = (byte)Math.Round(pct * 2.55);
            return true;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n is < 0 or > 255) return false;
        value = (byte)Math.Round(n);
        return true;
    }

    private static bool TryAlpha(string text, out double alpha)
    {
        alpha = 1;
        var isPct = text.EndsWith('%');
        if (!double.TryParse(isPct ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return false;
        alpha = isPct ? n / 100 : n;
        return alpha is >= 0 and <= 1;
    }

    private static string Format(byte r, byte g, byte b, byte a) =>
        a == 255 ? $"#{r:x2}{g:x2}{b:x2}" : $"#{a:x2}{r:x2}{g:x2}{b:x2}";

    [GeneratedRegex(@"^rgba?\(\s*(?<r>[\d.]+%?)\s*[,\s]\s*(?<g>[\d.]+%?)\s*[,\s]\s*(?<b>[\d.]+%?)\s*(?:[,/]\s*(?<a>[\d.]+%?)\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();
}

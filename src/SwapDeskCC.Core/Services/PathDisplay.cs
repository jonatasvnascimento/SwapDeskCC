namespace SwapDeskCC.Core.Services;

/// <summary>Encurta caminhos para exibição: %LOCALAPPDATA%\..., %USERPROFILE%\... ou ~/...</summary>
public static class PathDisplay
{
    public static string Shorten(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        if (OperatingSystem.IsWindows())
        {
            (string Folder, string Var)[] vars =
            [
                (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
                (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
            ];
            foreach (var (folder, name) in vars)
            {
                if (folder.Length > 0 && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    return name + path[folder.Length..];
            }
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.Ordinal) ? "~" + path[home.Length..] : path;
    }
}

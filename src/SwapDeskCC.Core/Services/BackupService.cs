using System.IO.Compression;

namespace SwapDeskCC.Core.Services;

/// <summary>Backup em .zip do perfil, sem caches (que o Claude recria sozinho).</summary>
public sealed class BackupService(string backupsRoot, int keepPerLabel = 3)
{
    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cache", "Code Cache", "GPUCache", "GPUPersistentCache", "GrShaderCache", "ShaderCache",
        "DawnGraphiteCache", "DawnWebGPUCache", "Crashpad", "logs", "Service Worker",
        "claude-code", // binários do Claude Code embutido, baixados de novo quando faltam
    };

    public string BackupsRoot { get; } = backupsRoot;

    public sealed record BackupInfo(string Path, int Files, int Skipped);

    public Task<BackupInfo> CreateAsync(string sourceDir, string label, CancellationToken ct = default) =>
        Task.Run(() => Create(sourceDir, label, ct), ct);

    private BackupInfo Create(string sourceDir, string label, CancellationToken ct)
    {
        Directory.CreateDirectory(BackupsRoot);
        var zipPath = Path.Combine(BackupsRoot, $"{label}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        int files = 0, skipped = 0;

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var file in EnumerateFiles(sourceDir))
            {
                ct.ThrowIfCancellationRequested();
                var entryName = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                try
                {
                    using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var output = zip.CreateEntry(entryName, CompressionLevel.Fastest).Open();
                    input.CopyTo(output);
                    files++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }
        }

        Prune(label);
        return new BackupInfo(zipPath, files, skipped);
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir).ToList(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                var info = new FileInfo(entry);
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    // Não entra em links dentro do perfil nem em pastas de cache.
                    if (info.LinkTarget is null && !SkippedDirs.Contains(info.Name)) pending.Push(entry);
                }
                else
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>Mantém só os N backups mais recentes de cada conta.</summary>
    private void Prune(string label)
    {
        var old = Directory.GetFiles(BackupsRoot, $"{label}-*.zip")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(keepPerLabel);
        foreach (var file in old) File.Delete(file);
    }
}

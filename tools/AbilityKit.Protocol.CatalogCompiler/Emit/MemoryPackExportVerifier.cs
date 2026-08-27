namespace AbilityKit.Protocol.CatalogCompiler.Emit;

public enum MemoryPackExportMismatchKind
{
    MissingFile,
    StaleFile,
    ExtraFile
}

public sealed record MemoryPackExportMismatch(
    MemoryPackExportMismatchKind Kind,
    string FileName,
    string Detail);

public sealed class MemoryPackExportVerification
{
    public IReadOnlyList<MemoryPackExportMismatch> Mismatches { get; }

    public bool IsCurrent => Mismatches.Count == 0;

    public MemoryPackExportVerification(IReadOnlyList<MemoryPackExportMismatch> mismatches)
    {
        Mismatches = mismatches;
    }
}

/// <summary>
/// Compares a deterministic in-memory export plan against the files committed in an export
/// folder. The comparison normalizes CRLF/LF because the repository has no .gitattributes and
/// core.autocrlf rewrites checked-out files, so byte-exact compares would depend on each
/// machine's git configuration rather than on the generated content itself.
/// </summary>
public static class MemoryPackExportVerifier
{
    public const string ManifestFileName = "protocol-export.json";

    /// <summary>
    /// Files the export owns inside its folder. Everything else (for example Unity .meta files)
    /// is ignored by the gate.
    /// </summary>
    public static bool IsManagedFile(string fileName) =>
        fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".proto", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase);

    public static MemoryPackExportVerification Verify(
        string directory,
        IReadOnlyDictionary<string, string> plannedFiles)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(plannedFiles);

        var mismatches = new List<MemoryPackExportMismatch>();
        foreach (var planned in plannedFiles.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, planned.Key);
            if (!File.Exists(path))
            {
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.MissingFile,
                    planned.Key,
                    "planned by the deterministic export but not committed"));
                continue;
            }

            var current = File.ReadAllText(path, System.Text.Encoding.UTF8);
            if (!string.Equals(NormalizeLineEndings(current), NormalizeLineEndings(planned.Value),
                    StringComparison.Ordinal))
            {
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.StaleFile,
                    planned.Key,
                    "committed content differs from the deterministic export"));
            }
        }

        if (Directory.Exists(directory))
        {
            var plannedNames = new HashSet<string>(
                plannedFiles.Keys, StringComparer.OrdinalIgnoreCase);
            var extraFiles = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => IsManagedFile(name) && !plannedNames.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            foreach (var extra in extraFiles)
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.ExtraFile,
                    extra,
                    "committed export file is no longer generated; remove it"));
        }

        mismatches.Sort((left, right) =>
        {
            var byKind = ((int)left.Kind).CompareTo((int)right.Kind);
            return byKind != 0 ? byKind : string.CompareOrdinal(left.FileName, right.FileName);
        });
        return new MemoryPackExportVerification(mismatches);
    }

    public static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
}

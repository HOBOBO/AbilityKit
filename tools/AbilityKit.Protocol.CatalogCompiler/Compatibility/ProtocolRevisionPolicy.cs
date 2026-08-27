using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// The catalog revision policy: breaking changes against the committed baseline must be
/// accompanied by a revision bump of the catalog that owns the broken contract, and no catalog
/// revision may ever decrease. Wire-schema breaking changes are attributed to every catalog whose
/// messages (in the baseline or the current sources) carry the affected payload type.
/// </summary>
public static class ProtocolRevisionPolicy
{
    public const string BreakingRequiresRevisionBump = "breaking-change-requires-revision-bump";
    public const string WireBreakingRequiresRevisionBump = "wire-breaking-change-requires-revision-bump";
    public const string RevisionMustNotDecrease = "revision-must-not-decrease";

    public static ProtocolRevisionPolicyResult Evaluate(
        ProtocolCompatibilityBaselineDocument baseline,
        IReadOnlyList<ProtocolCatalogIr> currentCatalogs,
        ProtocolCompatibilityReport report)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(currentCatalogs);
        ArgumentNullException.ThrowIfNull(report);

        var violations = new List<ProtocolRevisionPolicyViolation>();
        var baselineById = IndexBy(
            baseline.Catalogs,
            catalog => catalog.CatalogId,
            id => $"Duplicate baseline catalog id '{id}'.");
        var currentById = IndexBy(
            currentCatalogs,
            catalog => catalog.CatalogId,
            id => $"Duplicate current catalog id '{id}'.");

        foreach (var baselineCatalog in baseline.Catalogs)
        {
            if (!currentById.TryGetValue(baselineCatalog.CatalogId, out var currentCatalog))
                continue;
            if (currentCatalog.Revision < baselineCatalog.Revision)
                violations.Add(new ProtocolRevisionPolicyViolation(
                    baselineCatalog.CatalogId,
                    RevisionMustNotDecrease,
                    $"Catalog '{baselineCatalog.CatalogId}' revision decreased from " +
                    $"{baselineCatalog.Revision} to {currentCatalog.Revision}."));
        }

        foreach (var currentCatalog in currentCatalogs)
        {
            if (!baselineById.TryGetValue(currentCatalog.CatalogId, out var baselineCatalog))
                continue;

            var breakingCount = report.BreakingChanges.Count(change =>
                change.WireType == null &&
                string.Equals(change.CatalogId, currentCatalog.CatalogId, StringComparison.Ordinal));
            if (breakingCount > 0 && currentCatalog.Revision <= baselineCatalog.Revision)
                violations.Add(new ProtocolRevisionPolicyViolation(
                    currentCatalog.CatalogId,
                    BreakingRequiresRevisionBump,
                    $"Catalog '{currentCatalog.CatalogId}' carries {breakingCount} breaking change(s) but its revision " +
                    $"stayed at {currentCatalog.Revision} (baseline {baselineCatalog.Revision}); breaking changes " +
                    "must bump the catalog revision."));
        }

        foreach (var wireBreaking in report.BreakingChanges
                     .Where(change => change.WireType != null)
                     .GroupBy(change => change.WireType!, StringComparer.Ordinal))
        {
            var wireType = wireBreaking.Key;
            foreach (var catalogId in OwningCatalogIds(baseline.Catalogs, currentCatalogs, wireType))
            {
                if (!baselineById.TryGetValue(catalogId, out var baselineCatalog) ||
                    !currentById.TryGetValue(catalogId, out var currentCatalog))
                    continue;

                if (currentCatalog.Revision <= baselineCatalog.Revision)
                    violations.Add(new ProtocolRevisionPolicyViolation(
                        catalogId,
                        WireBreakingRequiresRevisionBump,
                        $"Breaking wire schema change(s) on '{wireType}' ({wireBreaking.Count()} finding(s)) require " +
                        $"catalog '{catalogId}' to bump its revision (currently {currentCatalog.Revision}, " +
                        $"baseline {baselineCatalog.Revision})."));
            }
        }

        return new ProtocolRevisionPolicyResult(violations);
    }

    /// <summary>
    /// Catalogs that reference the payload type either in the baseline or in the current sources;
    /// both directions matter because either side is the contract a peer was built against.
    /// </summary>
    private static IEnumerable<string> OwningCatalogIds(
        IReadOnlyList<ProtocolCompatibilityBaselineCatalog> baselineCatalogs,
        IReadOnlyList<ProtocolCatalogIr> currentCatalogs,
        string wireType)
    {
        var owners = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var catalog in baselineCatalogs)
        foreach (var message in catalog.Messages)
            if (string.Equals(MemoryPackExportPlanner.ElementType(message.PayloadType), wireType, StringComparison.Ordinal))
                owners.Add(catalog.CatalogId);
        foreach (var catalog in currentCatalogs)
        foreach (var message in catalog.Messages)
            if (string.Equals(MemoryPackExportPlanner.ElementType(message.PayloadType), wireType, StringComparison.Ordinal))
                owners.Add(catalog.CatalogId);
        return owners;
    }

    private static Dictionary<string, TValue> IndexBy<TValue>(
        IEnumerable<TValue> values,
        Func<TValue, string> key,
        Func<string, string> duplicateMessage)
    {
        var index = new Dictionary<string, TValue>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var id = key(value);
            if (!index.TryAdd(id, value))
                throw new InvalidDataException(duplicateMessage(id));
        }

        return index;
    }
}

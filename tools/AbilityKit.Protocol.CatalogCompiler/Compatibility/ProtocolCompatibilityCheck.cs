using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// Single entry point that combines the breaking diff with the revision policy, so callers
/// (editor workflows, CI, tests) get one verdict: a protocol change is clean only when it is
/// backwards-compatible or every breaking finding is covered by a catalog revision bump.
/// </summary>
public static class ProtocolCompatibilityCheck
{
    public static ProtocolCompatibilityCheckResult Check(
        ProtocolCompatibilityBaselineDocument baseline,
        IReadOnlyList<ProtocolCatalogIr> currentCatalogs,
        IReadOnlyList<WireSchemaIr> currentWireSchemas)
    {
        var report = ProtocolCompatibilityDiff.Compare(baseline, currentCatalogs, currentWireSchemas);
        var revisionPolicy = ProtocolRevisionPolicy.Evaluate(baseline, currentCatalogs, report);
        return new ProtocolCompatibilityCheckResult(report, revisionPolicy);
    }
}

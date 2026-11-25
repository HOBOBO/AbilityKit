using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// 合并破坏性差异与修订策略的统一入口。编辑器、CI 和测试获得同一结论：仅向后兼容，或每项破坏性差异均由所属目录修订号递增覆盖时，协议变更才通过。
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

using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// 兼容性基线采用的规范、编解码无关字符串形式。枚举按这些字符串保存，使序列化器或运行时枚举重命名不影响产物，并允许差异检查使用序数字符串相等比较。
/// </summary>
public static class ProtocolCompatibilityNames
{
    public static string Direction(IrDirection value) => value switch
    {
        IrDirection.ClientToServer => "c2s",
        IrDirection.ServerToClient => "s2c",
        _ => "bidirectional"
    };

    public static string Kind(IrPacketKind value) => value.ToString().ToLowerInvariant();

    public static string Reliability(IrReliability value) => value.ToString().ToLowerInvariant();

    public static string MemoryPackMode(WireMemoryPackMode value) => value.ToString().ToLowerInvariant();

    public static string Declaration(WireDeclarationKind value) => value.ToString().ToLowerInvariant();

    public static string MemberStyle(WireMemberStyle value) => value.ToString().ToLowerInvariant();
}

/// <summary>
/// 已检测协议兼容性变化的严重程度。
/// </summary>
public enum ProtocolCompatibilitySeverity
{
    /// <summary>
    /// 向后兼容，基于基线构建的对端仍可互操作。
    /// </summary>
    Compatible,

    /// <summary>
    /// 破坏性变化，基于基线构建的对端无法互操作。每项变化必须递增所属目录修订号，见 <see cref="ProtocolRevisionPolicy"/>。
    /// </summary>
    Breaking
}

/// <summary>
/// 兼容性差异检查可检测的协议变化类别。
/// </summary>
public enum ProtocolCompatibilityChangeKind
{
    /*
     * 目录范围。
     */
    CatalogAdded,
    CatalogRemoved,
    CatalogDefaultCodecChanged,

    /*
     * 消息范围，身份为目录 ID 与消息 ID。
     */
    MessageAdded,
    MessageRemoved,
    MessageOpCodeChanged,
    MessageOpCodeReassigned,
    MessageDirectionChanged,
    MessageKindChanged,
    MessagePayloadChanged,
    MessageCodecChanged,
    MessageResponseChanged,
    MessageReliabilityChanged,
    MessageSchemaWindowChanged,
    MessageBudgetChanged,

    /*
     * 线协议结构范围，身份为完整类型名。
     */
    WireSchemaAdded,
    WireSchemaRemoved,
    WireFieldAdded,
    WireFieldRemoved,
    WireFieldIdChanged,
    WireFieldRenamed,
    WireFieldTypeChanged,
    WireFieldRequirednessChanged,
    WireReservedIdConsumed,
    WireReservationChanged,
    WireMemoryPackModeChanged,
    WireDeclarationShapeChanged
}

/// <summary>
/// 基线与当前协议源之间的一项已分类差异。
/// </summary>
public sealed record ProtocolCompatibilityChange(
    string CatalogId,
    string? MessageId,
    string? WireType,
    ProtocolCompatibilityChangeKind Kind,
    ProtocolCompatibilitySeverity Severity,
    string Detail);

/// <summary>
/// 当前源与基线比较后的完整分类结果。
/// </summary>
public sealed class ProtocolCompatibilityReport
{
    public ProtocolCompatibilityReport(IReadOnlyList<ProtocolCompatibilityChange> changes)
    {
        Changes = changes ?? Array.Empty<ProtocolCompatibilityChange>();
        BreakingChanges = Changes
            .Where(change => change.Severity == ProtocolCompatibilitySeverity.Breaking)
            .ToArray();
    }

    public IReadOnlyList<ProtocolCompatibilityChange> Changes { get; }

    public IReadOnlyList<ProtocolCompatibilityChange> BreakingChanges { get; }

    /// <summary>
    /// 不存在破坏基线对端互操作能力的变化时为 true。
    /// </summary>
    public bool IsCompatible => BreakingChanges.Count == 0;
}

/// <summary>
/// 一项修订策略违规：破坏性变化未按要求递增，或修订号倒退。
/// </summary>
public sealed record ProtocolRevisionPolicyViolation(string CatalogId, string Rule, string Detail);

/// <summary>
/// 对兼容性报告应用目录修订策略的结果。
/// </summary>
public sealed record ProtocolRevisionPolicyResult(IReadOnlyList<ProtocolRevisionPolicyViolation> Violations)
{
    public ProtocolRevisionPolicyResult() : this(Array.Empty<ProtocolRevisionPolicyViolation>())
    {
    }

    public bool IsSatisfied => Violations.Count == 0;
}

/// <summary>
/// 合并兼容性结论，包括已分类差异及其修订策略结果。两部分均通过时协议变化才通过。
/// </summary>
public sealed record ProtocolCompatibilityCheckResult(
    ProtocolCompatibilityReport Report,
    ProtocolRevisionPolicyResult RevisionPolicy)
{
    public bool IsCompatible => Report.IsCompatible && RevisionPolicy.IsSatisfied;
}

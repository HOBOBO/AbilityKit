using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// Canonical, codec-neutral string forms used by the compatibility baseline artifact. The
/// baseline stores enums as these strings so the artifact is stable across serializer and
/// runtime enum renames, and the diff can compare with plain ordinal string equality.
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

/// <summary>Severity of a detected protocol compatibility change.</summary>
public enum ProtocolCompatibilitySeverity
{
    /// <summary>Backwards-compatible: peers built against the baseline keep interoperating.</summary>
    Compatible,

    /// <summary>
    /// Breaking: peers built against the baseline cannot interoperate. Every breaking change
    /// must be accompanied by a revision bump of the owning catalog (see
    /// <see cref="ProtocolRevisionPolicy"/>).
    /// </summary>
    Breaking
}

/// <summary>Classified kinds of protocol changes the compatibility diff can detect.</summary>
public enum ProtocolCompatibilityChangeKind
{
    // Catalog scope
    CatalogAdded,
    CatalogRemoved,
    CatalogDefaultCodecChanged,

    // Message scope (identity: catalog id + message id)
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

    // Wire schema scope (identity: qualified type)
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

/// <summary>One classified difference between a baseline and the current protocol sources.</summary>
public sealed record ProtocolCompatibilityChange(
    string CatalogId,
    string? MessageId,
    string? WireType,
    ProtocolCompatibilityChangeKind Kind,
    ProtocolCompatibilitySeverity Severity,
    string Detail);

/// <summary>The full classified result of comparing current sources against a baseline.</summary>
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

    /// <summary>True when no change breaks peers built against the baseline.</summary>
    public bool IsCompatible => BreakingChanges.Count == 0;
}

/// <summary>A single revision-policy violation (breaking change without the required bump, or a regression).</summary>
public sealed record ProtocolRevisionPolicyViolation(string CatalogId, string Rule, string Detail);

/// <summary>Outcome of evaluating the catalog revision policy over a compatibility report.</summary>
public sealed record ProtocolRevisionPolicyResult(IReadOnlyList<ProtocolRevisionPolicyViolation> Violations)
{
    public ProtocolRevisionPolicyResult() : this(Array.Empty<ProtocolRevisionPolicyViolation>())
    {
    }

    public bool IsSatisfied => Violations.Count == 0;
}

/// <summary>
/// Combined compatibility verdict: the classified diff plus the revision policy evaluation over
/// that diff. A protocol change is clean only when both parts pass.
/// </summary>
public sealed record ProtocolCompatibilityCheckResult(
    ProtocolCompatibilityReport Report,
    ProtocolRevisionPolicyResult RevisionPolicy)
{
    public bool IsCompatible => Report.IsCompatible && RevisionPolicy.IsSatisfied;
}

namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>Codec-neutral scalar names accepted by the minimal wire schema.</summary>
public static class WireScalarTypes
{
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "uint8", "int32", "int64", "uint32", "uint64", "float", "double", "string", "bytes"
    };
}

public enum WireMemoryPackMode
{
    VersionTolerant,
    Sequential
}

public enum WireDeclarationKind
{
    Class,
    Struct
}

public enum WireMemberStyle
{
    Property,
    Field
}

public static class WireSchemaFormatVersions
{
    public const int Current = 2;
}

/// <summary>Field-level schema independent of any business DTO or serialization library.</summary>
public sealed class WireFieldIr
{
    public WireFieldIr(
        uint id,
        string name,
        string scalarType,
        bool isArray,
        bool isOptional,
        string? typeName = null,
        bool isExternalReference = false)
    {
        Id = id;
        Name = name ?? string.Empty;
        ScalarType = scalarType ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        IsArray = isArray;
        IsOptional = isOptional;
        IsExternalReference = isExternalReference;
    }

    public uint Id { get; }
    public string Name { get; }
    public string ScalarType { get; }
    public string TypeName { get; }
    public bool IsCustomType => TypeName.Length > 0;
    /// <summary>True when the referenced type is owned and compiled outside this wire export.</summary>
    public bool IsExternalReference { get; }
    public bool IsArray { get; }
    public bool IsOptional { get; }
    public bool IsRequired => !IsOptional;
}

/// <summary>Minimal field-level wire schema IR produced by a source parser.</summary>
public sealed class WireSchemaIr
{
    public WireSchemaIr(
        int schemaVersion,
        string type,
        IReadOnlyList<WireFieldIr> fields,
        IReadOnlyList<uint> reservedIds,
        string? projectId = null,
        string? targetNamespace = null,
        WireMemoryPackMode memoryPackMode = WireMemoryPackMode.VersionTolerant,
        WireDeclarationKind declarationKind = WireDeclarationKind.Class,
        WireMemberStyle memberStyle = WireMemberStyle.Property,
        string? groupId = null)
    {
        SchemaVersion = schemaVersion;
        Type = type ?? string.Empty;
        Fields = fields ?? Array.Empty<WireFieldIr>();
        ReservedIds = reservedIds ?? Array.Empty<uint>();
        ProjectId = projectId ?? string.Empty;
        TargetNamespace = targetNamespace ?? string.Empty;
        MemoryPackMode = memoryPackMode;
        DeclarationKind = declarationKind;
        MemberStyle = memberStyle;
        GroupId = groupId ?? string.Empty;
    }

    public int SchemaVersion { get; }
    public string ProjectId { get; }
    public string TargetNamespace { get; }
    public WireMemoryPackMode MemoryPackMode { get; }
    public WireDeclarationKind DeclarationKind { get; }
    public WireMemberStyle MemberStyle { get; }
    public string GroupId { get; }
    public string Type { get; }
    public IReadOnlyList<WireFieldIr> Fields { get; }
    public IReadOnlyList<uint> ReservedIds { get; }
}

/// <summary>A wire source document and the independently exported types it contains.</summary>
public sealed class WireSchemaDocumentIr
{
    public WireSchemaDocumentIr(
        int schemaVersion,
        string projectId,
        string targetNamespace,
        string groupId,
        WireMemoryPackMode defaultMemoryPackMode,
        WireDeclarationKind defaultDeclarationKind,
        WireMemberStyle defaultMemberStyle,
        IReadOnlyList<WireSchemaIr> schemas)
    {
        SchemaVersion = schemaVersion;
        ProjectId = projectId ?? string.Empty;
        TargetNamespace = targetNamespace ?? string.Empty;
        GroupId = groupId ?? string.Empty;
        DefaultMemoryPackMode = defaultMemoryPackMode;
        DefaultDeclarationKind = defaultDeclarationKind;
        DefaultMemberStyle = defaultMemberStyle;
        Schemas = schemas ?? Array.Empty<WireSchemaIr>();
    }

    public int SchemaVersion { get; }
    public string ProjectId { get; }
    public string TargetNamespace { get; }
    public string GroupId { get; }
    public WireMemoryPackMode DefaultMemoryPackMode { get; }
    public WireDeclarationKind DefaultDeclarationKind { get; }
    public WireMemberStyle DefaultMemberStyle { get; }
    public IReadOnlyList<WireSchemaIr> Schemas { get; }
}

public interface IWireSchemaParser
{
    WireSchemaIr Parse(string sourcePath, string sourceText);
    WireSchemaDocumentIr ParseDocument(string sourcePath, string sourceText);
}

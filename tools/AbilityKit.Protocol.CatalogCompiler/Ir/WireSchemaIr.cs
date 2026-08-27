namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>Codec-neutral scalar names accepted by the minimal wire schema.</summary>
public static class WireScalarTypes
{
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "int32", "int64", "uint32", "uint64", "float", "double", "string", "bytes"
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

/// <summary>Field-level schema independent of any business DTO or serialization library.</summary>
public sealed class WireFieldIr
{
    public WireFieldIr(
        uint id,
        string name,
        string scalarType,
        bool isArray,
        bool isOptional,
        string? typeName = null)
    {
        Id = id;
        Name = name ?? string.Empty;
        ScalarType = scalarType ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        IsArray = isArray;
        IsOptional = isOptional;
    }

    public uint Id { get; }
    public string Name { get; }
    public string ScalarType { get; }
    public string TypeName { get; }
    public bool IsCustomType => TypeName.Length > 0;
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
        WireMemberStyle memberStyle = WireMemberStyle.Property)
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
    }

    public int SchemaVersion { get; }
    public string ProjectId { get; }
    public string TargetNamespace { get; }
    public WireMemoryPackMode MemoryPackMode { get; }
    public WireDeclarationKind DeclarationKind { get; }
    public WireMemberStyle MemberStyle { get; }
    public string Type { get; }
    public IReadOnlyList<WireFieldIr> Fields { get; }
    public IReadOnlyList<uint> ReservedIds { get; }
}

public interface IWireSchemaParser
{
    WireSchemaIr Parse(string sourcePath, string sourceText);
}

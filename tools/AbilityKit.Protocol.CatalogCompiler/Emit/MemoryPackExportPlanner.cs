using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>
/// 构建 MemoryPack 导出所需、由项目拥有的传递结构依赖闭包。
/// </summary>
public static class MemoryPackExportPlanner
{
    public static MemoryPackExportPlan Create(
        IReadOnlyCollection<string> referencedTypes,
        IReadOnlyCollection<WireSchemaIr> ownedSchemas,
        bool includeUnreferenced)
    {
        ArgumentNullException.ThrowIfNull(referencedTypes);
        ArgumentNullException.ThrowIfNull(ownedSchemas);

        var schemasByType = new Dictionary<string, WireSchemaIr>(StringComparer.Ordinal);
        foreach (var schema in ownedSchemas)
        {
            var qualifiedType = QualifiedType(schema);
            if (!schemasByType.TryAdd(qualifiedType, schema))
                throw new InvalidDataException($"Duplicate owned wire schema type '{qualifiedType}'.");
        }

        var pending = new SortedSet<string>(referencedTypes, StringComparer.Ordinal);
        if (includeUnreferenced)
        {
            foreach (var qualifiedType in schemasByType.Keys) pending.Add(qualifiedType);
        }

        var selected = new Dictionary<string, WireSchemaIr>(StringComparer.Ordinal);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var qualifiedType = pending.Min!;
            pending.Remove(qualifiedType);
            if (qualifiedType.StartsWith("System.", StringComparison.Ordinal) ||
                selected.ContainsKey(qualifiedType) ||
                missing.Contains(qualifiedType))
                continue;

            if (!schemasByType.TryGetValue(qualifiedType, out var schema))
            {
                missing.Add(qualifiedType);
                continue;
            }

            selected.Add(qualifiedType, schema);
            foreach (var dependency in schema.Fields
                         .Where(field => field.IsCustomType && !field.IsExternalReference)
                         .Select(field => field.TypeName))
                pending.Add(dependency);
        }

        return new MemoryPackExportPlan(
            selected.OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => value.Value)
                .ToArray(),
            missing.ToArray());
    }

    public static string QualifiedType(WireSchemaIr schema) =>
        string.IsNullOrWhiteSpace(schema.TargetNamespace)
            ? schema.Type
            : schema.TargetNamespace + "." + schema.Type;

    /// <summary>
    /// 移除目录载荷类型末尾的 <c>[]</c>，得到线协议完整类型名对应的元素类型。保留 <c>System.*</c> 基本类型，使规划器继续将其作为无需结构定义的标量忽略。
    /// </summary>
    public static string ElementType(string payloadType)
    {
        var value = payloadType?.Trim() ?? string.Empty;
        while (value.EndsWith("[]", StringComparison.Ordinal))
            value = value.Substring(0, value.Length - 2);
        return value;
    }
}

public sealed record MemoryPackExportPlan(
    IReadOnlyList<WireSchemaIr> Schemas,
    IReadOnlyList<string> MissingTypes);

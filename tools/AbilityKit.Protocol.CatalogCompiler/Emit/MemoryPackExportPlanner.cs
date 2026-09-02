using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>Builds the transitive, project-owned schema closure required by a MemoryPack export.</summary>
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
    /// Strips trailing <c>[]</c> from a catalog payload type to recover the element type,
    /// which is what the wire-schema qualified type names. <c>System.*</c> primitives are left
    /// untouched so the planner can keep ignoring them as schema-less scalars.
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

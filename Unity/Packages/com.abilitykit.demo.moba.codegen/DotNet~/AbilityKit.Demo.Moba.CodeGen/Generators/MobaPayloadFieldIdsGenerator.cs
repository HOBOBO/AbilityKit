using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [Generator]
    public sealed class MobaPayloadFieldIdsGenerator : ISourceGenerator
    {
        private const string AccessorAttributeMetadataName = "AbilityKit.Demo.Moba.GeneratePayloadAccessorAttribute";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new PayloadFieldIdsSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is PayloadFieldIdsSyntaxReceiver receiver))
            {
                return;
            }

            var attributeType = context.Compilation.GetTypeByMetadataName(
                MobaPayloadFieldIdsContract.AttributeMetadataName);
            var accessorAttributeType = context.Compilation.GetTypeByMetadataName(AccessorAttributeMetadataName);
            if (attributeType == null && accessorAttributeType == null)
            {
                return;
            }

            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in receiver.Types)
            {
                if (!seen.Add(type))
                {
                    continue;
                }

                var declaredAttributes = type.GetAttributes();
                var attributes = attributeType == null
                    ? Array.Empty<AttributeData>()
                    : declaredAttributes.Where(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType)).ToArray();
                var accessorAttributes = accessorAttributeType == null
                    ? Array.Empty<AttributeData>()
                    : declaredAttributes.Where(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, accessorAttributeType)).ToArray();
                if (attributes.Length == 0 && accessorAttributes.Length == 0)
                {
                    continue;
                }

                if (attributes.Length > 0)
                {
                    var validation = MobaPayloadFieldIdsContract.Validate(
                        context.Compilation,
                        type,
                        attributes);
                    if (validation.IsValid)
                    {
                        context.AddSource(GetHintName(type), GenerateSource(type, validation.Groups));
                    }
                }

                if (accessorAttributes.Length > 0)
                {
                    if (TryBuildAccessorMappings(type, accessorAttributes, out var mappings, out var error))
                    {
                        context.AddSource(GetAccessorHintName(type), GenerateAccessorSource(type, mappings));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MobaDiagnosticRules.InvalidPayloadFieldIdsDeclarationRule,
                            type.Locations.FirstOrDefault(),
                            type.ToDisplayString(),
                            error));
                    }
                }
            }
        }

        private static bool TryBuildAccessorMappings(
            INamedTypeSymbol accessorType,
            IReadOnlyList<AttributeData> attributes,
            out IReadOnlyList<PayloadAccessorMapping> mappings,
            out string error)
        {
            var result = new List<PayloadAccessorMapping>(attributes.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Length != 6 ||
                    !(attribute.ConstructorArguments[0].Value is INamedTypeSymbol payloadType) ||
                    !(attribute.ConstructorArguments[1].Value is INamedTypeSymbol catalogType) ||
                    !(attribute.ConstructorArguments[2].Value is string fieldName) ||
                    !(attribute.ConstructorArguments[3].Value is string sourceMember) ||
                    !(attribute.ConstructorArguments[4].Value is int valueKind) ||
                    !(attribute.ConstructorArguments[5].Value is bool includeLegacyId))
                {
                    mappings = Array.Empty<PayloadAccessorMapping>();
                    error = "payload accessor constructor arguments could not be resolved";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(sourceMember))
                {
                    mappings = Array.Empty<PayloadAccessorMapping>();
                    error = "payload field name and source member are required";
                    return false;
                }

                var catalogField = catalogType.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault();
                if (catalogField == null || !catalogField.IsConst || catalogField.Type.SpecialType != SpecialType.System_String)
                {
                    mappings = Array.Empty<PayloadAccessorMapping>();
                    error = $"'{catalogType.Name}.{fieldName}' must be a const string field";
                    return false;
                }

                var source = payloadType.GetMembers(sourceMember).FirstOrDefault(member =>
                    member is IFieldSymbol || member is IPropertySymbol);
                var sourceType = source is IFieldSymbol field ? field.Type : (source as IPropertySymbol)?.Type;
                if (sourceType == null || !IsValueKindCompatible(valueKind, sourceType))
                {
                    mappings = Array.Empty<PayloadAccessorMapping>();
                    error = $"source member '{payloadType.Name}.{sourceMember}' is missing or incompatible with value kind {valueKind}";
                    return false;
                }

                var key = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ":" + fieldName;
                if (!keys.Add(key))
                {
                    mappings = Array.Empty<PayloadAccessorMapping>();
                    error = $"payload field '{fieldName}' is mapped more than once for '{payloadType.Name}'";
                    return false;
                }

                result.Add(new PayloadAccessorMapping(payloadType, catalogType, fieldName, sourceMember, valueKind, includeLegacyId));
            }

            mappings = result;
            error = null!;
            return true;
        }

        private static bool IsValueKindCompatible(int kind, ITypeSymbol type)
        {
            switch (kind)
            {
                case 0:
                case 5: return type.SpecialType == SpecialType.System_Int32;
                case 1: return type.SpecialType == SpecialType.System_Double || type.SpecialType == SpecialType.System_Single;
                case 2: return type.TypeKind == TypeKind.Enum;
                case 3: return type.SpecialType == SpecialType.System_Boolean;
                case 4: return type.Name == "Fixed64";
                default: return false;
            }
        }

        private static string GenerateAccessorSource(
            INamedTypeSymbol accessorType,
            IReadOnlyList<PayloadAccessorMapping> mappings)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            if (!accessorType.ContainingNamespace.IsGlobalNamespace)
            {
                source.Append("namespace ").Append(accessorType.ContainingNamespace.ToDisplayString()).AppendLine();
                source.AppendLine("{");
            }

            var indent = accessorType.ContainingNamespace.IsGlobalNamespace ? string.Empty : "    ";
            source.Append(indent).Append(GetAccessibility(accessorType.DeclaredAccessibility))
                .Append(GetTypeModifiers(accessorType)).Append(" partial class ").Append(accessorType.Name).AppendLine();
            source.Append(indent).AppendLine("{");
            foreach (var group in mappings.GroupBy<PayloadAccessorMapping, INamedTypeSymbol>(
                             item => item.PayloadType,
                             SymbolEqualityComparer.Default)
                         .OrderBy(item => item.Key.ToDisplayString(), StringComparer.Ordinal))
            {
                var payloadType = group.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var isReferenceType = group.Key.IsReferenceType;
                source.Append(indent).Append("    public bool TryGet(in ").Append(payloadType).AppendLine(" args, int fieldId, out int value)");
                source.Append(indent).AppendLine("    {");
                source.Append(indent).AppendLine("        value = 0;");
                if (isReferenceType) source.Append(indent).AppendLine("        if (args == null) return false;");
                foreach (var mapping in group)
                {
                    source.Append(indent).Append("        if (").Append(BuildFieldMatch(mapping)).AppendLine(")");
                    source.Append(indent).AppendLine("        {");
                    source.Append(indent).Append("            value = ").Append(BuildIntValue(mapping)).AppendLine(";");
                    source.Append(indent).AppendLine("            return true;");
                    source.Append(indent).AppendLine("        }");
                }
                source.Append(indent).AppendLine("        return false;");
                source.Append(indent).AppendLine("    }");
                source.AppendLine();
                source.Append(indent).Append("    public bool TryGet(in ").Append(payloadType).AppendLine(" args, int fieldId, out double value)");
                source.Append(indent).AppendLine("    {");
                source.Append(indent).AppendLine("        value = 0d;");
                if (isReferenceType) source.Append(indent).AppendLine("        if (args == null) return false;");
                foreach (var mapping in group)
                {
                    source.Append(indent).Append("        if (").Append(BuildFieldMatch(mapping)).AppendLine(")");
                    source.Append(indent).AppendLine("        {");
                    source.Append(indent).Append("            value = ").Append(BuildDoubleValue(mapping)).AppendLine(";");
                    source.Append(indent).AppendLine("            return true;");
                    source.Append(indent).AppendLine("        }");
                }
                source.Append(indent).AppendLine("        return false;");
                source.Append(indent).AppendLine("    }");
            }
            source.Append(indent).AppendLine("}");
            if (!accessorType.ContainingNamespace.IsGlobalNamespace) source.AppendLine("}");
            return source.ToString();
        }

        private static string BuildFieldMatch(PayloadAccessorMapping mapping)
        {
            var value = "fieldId == " + mapping.FieldName + "Id";
            return mapping.IncludeLegacyId ? value + " || fieldId == " + mapping.FieldName + "LegacyId" : value;
        }

        private static string BuildIntValue(PayloadAccessorMapping mapping)
        {
            var member = "args." + mapping.SourceMember;
            switch (mapping.ValueKind)
            {
                case 0: return member;
                case 1: return "(int)global::System.Math.Round((double)" + member + ")";
                case 2: return "(int)" + member;
                case 3: return member + " ? 1 : 0";
                case 4: return "(int)global::System.Math.Round((double)" + member + ".Value)";
                case 5: return "global::System.Math.Max(1, " + member + ")";
                default: throw new InvalidOperationException("Unsupported payload accessor value kind.");
            }
        }

        private static string BuildDoubleValue(PayloadAccessorMapping mapping)
        {
            var member = "args." + mapping.SourceMember;
            switch (mapping.ValueKind)
            {
                case 0: return member;
                case 1: return "(double)" + member;
                case 2: return "(int)" + member;
                case 3: return member + " ? 1d : 0d";
                case 4: return "(double)" + member + ".Value";
                case 5: return "global::System.Math.Max(1, " + member + ")";
                default: throw new InvalidOperationException("Unsupported payload accessor value kind.");
            }
        }

        private static string GetAccessorHintName(INamedTypeSymbol type)
        {
            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty)
                .Replace('.', '_');
            return name + ".PayloadAccessors.g.cs";
        }

        private static string GenerateSource(
            INamedTypeSymbol type,
            IReadOnlyList<MobaPayloadFieldGroup> groups)
        {
            var fields = new Dictionary<string, GeneratedField>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                foreach (var field in group.Fields)
                {
                    if (fields.TryGetValue(field.Name, out var existing))
                    {
                        if (!string.Equals(existing.CatalogType, group.CatalogTypeName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        existing.IncludeLegacyIds |= field.IncludeLegacyIds;
                    }
                    else
                    {
                        fields.Add(field.Name, new GeneratedField(
                            group.CatalogTypeName,
                            field.Name,
                            field.IncludeLegacyIds));
                    }
                }
            }

            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                source.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).AppendLine();
                source.AppendLine("{");
            }

            var indent = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : "    ";
            source.Append(indent)
                .Append(GetAccessibility(type.DeclaredAccessibility))
                .Append(GetTypeModifiers(type))
                .Append(" partial class ")
                .Append(type.Name)
                .AppendLine();
            source.Append(indent).AppendLine("{");

            foreach (var field in fields.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                source.Append(indent).Append("    private static readonly int ")
                    .Append(field.Name).Append("Id = ")
                    .Append(field.CatalogType).Append(".FieldId(")
                    .Append(field.CatalogType).Append('.').Append(field.Name).AppendLine(");");
                if (field.IncludeLegacyIds)
                {
                    source.Append(indent).Append("    private static readonly int ")
                        .Append(field.Name).Append("LegacyId = ")
                        .Append(field.CatalogType).Append(".LegacyFieldId(")
                        .Append(field.CatalogType).Append('.').Append(field.Name).AppendLine(");");
                }
            }

            foreach (var group in groups.OrderBy(item => item.MethodName, StringComparer.Ordinal))
            {
                source.AppendLine();
                source.Append(indent).Append("    public static bool ")
                    .Append(group.MethodName).AppendLine("(int fieldId)");
                source.Append(indent).AppendLine("    {");
                source.Append(indent).Append("        return ");
                for (var index = 0; index < group.Fields.Count; index++)
                {
                    var field = group.Fields[index];
                    if (index > 0)
                    {
                        source.AppendLine().Append(indent).Append("            || ");
                    }

                    source.Append("fieldId == ").Append(field.Name).Append("Id");
                    if (field.IncludeLegacyIds)
                    {
                        source.Append(" || fieldId == ").Append(field.Name).Append("LegacyId");
                    }
                }

                source.AppendLine(";");
                source.Append(indent).AppendLine("    }");
            }

            source.Append(indent).AppendLine("}");
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                source.AppendLine("}");
            }

            return source.ToString();
        }

        private static string GetAccessibility(Accessibility accessibility)
        {
            return accessibility == Accessibility.Public ? "public" : "internal";
        }

        private static string GetTypeModifiers(INamedTypeSymbol type)
        {
            if (type.IsStatic) return " static";
            if (type.IsSealed) return " sealed";
            if (type.IsAbstract) return " abstract";
            return string.Empty;
        }

        private static string GetHintName(INamedTypeSymbol type)
        {
            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty)
                .Replace('.', '_');
            return name + ".PayloadFieldIds.g.cs";
        }

        private sealed class GeneratedField
        {
            public GeneratedField(string catalogType, string name, bool includeLegacyIds)
            {
                CatalogType = catalogType;
                Name = name;
                IncludeLegacyIds = includeLegacyIds;
            }

            public string CatalogType { get; }
            public string Name { get; }
            public bool IncludeLegacyIds { get; set; }
        }

        private sealed class PayloadAccessorMapping
        {
            public PayloadAccessorMapping(
                INamedTypeSymbol payloadType,
                INamedTypeSymbol catalogType,
                string fieldName,
                string sourceMember,
                int valueKind,
                bool includeLegacyId)
            {
                PayloadType = payloadType;
                CatalogType = catalogType;
                FieldName = fieldName;
                SourceMember = sourceMember;
                ValueKind = valueKind;
                IncludeLegacyId = includeLegacyId;
            }

            public INamedTypeSymbol PayloadType { get; }
            public INamedTypeSymbol CatalogType { get; }
            public string FieldName { get; }
            public string SourceMember { get; }
            public int ValueKind { get; }
            public bool IncludeLegacyId { get; }
        }
    }

    internal sealed class PayloadFieldIdsSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> Types { get; } = new List<INamedTypeSymbol>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0 &&
                context.SemanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol type)
            {
                Types.Add(type);
            }
        }
    }
}

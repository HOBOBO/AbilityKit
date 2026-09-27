using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [Generator]
    public sealed class MobaPlanActionSchemaGenerator : ISourceGenerator
    {
        private const string SchemaAttributeName = "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.GenerateMobaPlanActionSchemaAttribute";
        private const string ArgAttributeName = "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.MobaPlanActionArgAttribute";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new Receiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is Receiver receiver)) return;
            var schemaAttributeType = context.Compilation.GetTypeByMetadataName(SchemaAttributeName);
            var argAttributeType = context.Compilation.GetTypeByMetadataName(ArgAttributeName);
            if (schemaAttributeType == null || argAttributeType == null) return;

            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var argsType in receiver.Types)
            {
                if (!seen.Add(argsType)) continue;
                var schemaAttribute = argsType.GetAttributes().FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.AttributeClass, schemaAttributeType));
                if (schemaAttribute == null) continue;

                if (!TryBuildModel(argsType, schemaAttribute, argAttributeType, out var model, out var error))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidPlanActionSchemaRule,
                        argsType.Locations.FirstOrDefault(),
                        argsType.ToDisplayString(),
                        error));
                    continue;
                }

                context.AddSource(model.HintName, GenerateSource(model));
            }
        }

        private static bool TryBuildModel(
            INamedTypeSymbol argsType,
            AttributeData schemaAttribute,
            INamedTypeSymbol argAttributeType,
            out SchemaModel model,
            out string error)
        {
            model = null!;
            error = null!;
            if (argsType.TypeKind != TypeKind.Struct || argsType.IsGenericType || argsType.ContainingType != null)
            {
                error = "args type must be a non-generic top-level struct";
                return false;
            }

            var actionName = schemaAttribute.ConstructorArguments.Length == 1
                ? schemaAttribute.ConstructorArguments[0].Value as string
                : null;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                error = "action name is required";
                return false;
            }

            var schemaName = argsType.Name.EndsWith("Args", StringComparison.Ordinal)
                ? argsType.Name.Substring(0, argsType.Name.Length - 4) + "Schema"
                : argsType.Name + "Schema";
            var schemaType = argsType.ContainingNamespace.GetTypeMembers(schemaName).FirstOrDefault();
            if (schemaType == null || !schemaType.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<ClassDeclarationSyntax>()
                    .Any(declaration => declaration.Modifiers.Any(item => item.Text == "partial")))
            {
                error = $"anchor type '{schemaName}' must exist and be partial";
                return false;
            }

            var constructors = argsType.InstanceConstructors
                .Where(ctor => !ctor.IsImplicitlyDeclared && ctor.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(ctor => ctor.Parameters.Length)
                .ToArray();
            if (constructors.Length != 1)
            {
                error = "args type must declare exactly one public constructor";
                return false;
            }

            var parameters = new List<ArgModel>();
            foreach (var parameter in constructors[0].Parameters)
            {
                var attribute = parameter.GetAttributes().FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.AttributeClass, argAttributeType));
                if (attribute == null)
                {
                    error = $"constructor parameter '{parameter.Name}' is missing MobaPlanActionArgAttribute";
                    return false;
                }

                if (!TryBuildArg(parameter, attribute, out var arg, out error)) return false;
                parameters.Add(arg);
            }

            model = new SchemaModel(argsType, schemaName, actionName!, parameters);
            return true;
        }

        private static bool TryBuildArg(
            IParameterSymbol parameter,
            AttributeData attribute,
            out ArgModel model,
            out string error)
        {
            model = null!;
            error = null!;
            if (attribute.ConstructorArguments.Length != 4 ||
                !(attribute.ConstructorArguments[0].Value is int kind) ||
                !(attribute.ConstructorArguments[1].Value is double defaultValue) ||
                !(attribute.ConstructorArguments[2].Value is bool required) ||
                attribute.ConstructorArguments[3].Kind != TypedConstantKind.Array)
            {
                error = $"attribute on '{parameter.Name}' has unresolved constructor arguments";
                return false;
            }

            var aliases = attribute.ConstructorArguments[3].Values
                .Select(item => item.Value as string)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (aliases.Length == 0)
            {
                error = $"argument '{parameter.Name}' requires at least one alias";
                return false;
            }

            if (!IsKindCompatible(kind, parameter.Type))
            {
                error = $"argument '{parameter.Name}' kind is incompatible with '{parameter.Type.ToDisplayString()}'";
                return false;
            }

            var displayName = aliases[0];
            var min = double.NaN;
            var max = double.NaN;
            var validateEnum = true;
            foreach (var pair in attribute.NamedArguments)
            {
                if (pair.Key == "DisplayName" && pair.Value.Value is string name && !string.IsNullOrWhiteSpace(name)) displayName = name;
                else if (pair.Key == "Min" && pair.Value.Value is double minValue) min = minValue;
                else if (pair.Key == "Max" && pair.Value.Value is double maxValue) max = maxValue;
                else if (pair.Key == "ValidateEnum" && pair.Value.Value is bool enumValue) validateEnum = enumValue;
            }

            if (!double.IsNaN(min) && !double.IsNaN(max) && min > max)
            {
                error = $"argument '{parameter.Name}' has min greater than max";
                return false;
            }

            model = new ArgModel(parameter, kind, defaultValue, required, aliases, displayName, min, max, validateEnum);
            return true;
        }

        private static bool IsKindCompatible(int kind, ITypeSymbol type)
        {
            switch (kind)
            {
                case 0: return type.SpecialType == SpecialType.System_Int32;
                case 1: return type.SpecialType == SpecialType.System_Single || type.SpecialType == SpecialType.System_Double;
                case 2:
                case 3: return type.SpecialType == SpecialType.System_Boolean;
                case 4: return type.TypeKind == TypeKind.Enum;
                default: return false;
            }
        }

        private static string GenerateSource(SchemaModel model)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            if (!model.ArgsType.ContainingNamespace.IsGlobalNamespace)
            {
                source.Append("namespace ").Append(model.ArgsType.ContainingNamespace.ToDisplayString()).AppendLine();
                source.AppendLine("{");
            }

            var indent = model.ArgsType.ContainingNamespace.IsGlobalNamespace ? string.Empty : "    ";
            source.Append(indent).Append("public sealed partial class ").Append(model.SchemaName)
                .Append(" : MobaPlanActionSchemaBase<").Append(model.ArgsTypeName).AppendLine(">");
            source.Append(indent).AppendLine("{");
            source.Append(indent).Append("    public static readonly ").Append(model.SchemaName).Append(" Instance = new ")
                .Append(model.SchemaName).AppendLine("();");
            source.Append(indent).Append("    protected override string ActionName => \"").Append(Escape(model.ActionName)).AppendLine("\";");
            source.AppendLine();
            source.Append(indent).Append("    public override ").Append(model.ArgsTypeName)
                .AppendLine(" ParseArgs(global::System.Collections.Generic.Dictionary<string, global::AbilityKit.Triggering.Runtime.Plan.ActionArgValue> namedArgs, global::AbilityKit.Triggering.Runtime.ExecCtx<global::AbilityKit.Ability.World.DI.IWorldResolver> ctx)");
            source.Append(indent).AppendLine("    {");
            source.Append(indent).Append("        return new ").Append(model.ArgsTypeName).AppendLine("(");
            for (var i = 0; i < model.Args.Count; i++)
            {
                source.Append(indent).Append("            ").Append(BuildReadExpression(model.Args[i]));
                source.AppendLine(i + 1 == model.Args.Count ? ");" : ",");
            }
            source.Append(indent).AppendLine("    }");
            source.AppendLine();
            source.Append(indent).AppendLine("    public override bool TryValidateArgs(global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, global::AbilityKit.Triggering.Runtime.Plan.ActionArgValue>> args, out string error)");
            source.Append(indent).AppendLine("    {");
            foreach (var arg in model.Args)
            {
                var aliases = BuildAliases(arg.Aliases);
                if (arg.Required)
                {
                    source.Append(indent).Append("        if (!RequireNumericValue(args, \"").Append(Escape(arg.DisplayName))
                        .Append("\", out error, ").Append(aliases).AppendLine(")) return false;");
                }
                if (!double.IsNaN(arg.Min) || !double.IsNaN(arg.Max))
                {
                    source.Append(indent).Append("        if (!ValidateNumericRange(args, \"").Append(Escape(arg.DisplayName))
                        .Append("\", ").Append(Literal(arg.Min)).Append(", ").Append(Literal(arg.Max))
                        .Append(", out error, ").Append(aliases).AppendLine(")) return false;");
                }
                if (arg.Kind == 4 && arg.ValidateEnum)
                {
                    source.Append(indent).Append("        if (!ValidateEnumValue<").Append(arg.TypeName).Append(">(args, \"")
                        .Append(Escape(arg.DisplayName)).Append("\", out error, ").Append(aliases).AppendLine(")) return false;");
                }
            }
            source.Append(indent).AppendLine("        error = null;");
            source.Append(indent).AppendLine("        return true;");
            source.Append(indent).AppendLine("    }");
            source.Append(indent).AppendLine("}");
            if (!model.ArgsType.ContainingNamespace.IsGlobalNamespace) source.AppendLine("}");
            return source.ToString();
        }

        private static string BuildReadExpression(ArgModel arg)
        {
            var aliases = BuildAliases(arg.Aliases);
            switch (arg.Kind)
            {
                case 0: return $"ReadInt(namedArgs, ctx, {(int)Math.Round(arg.DefaultValue)}, {aliases})";
                case 1:
                    var read = $"ReadFloat(namedArgs, ctx, {LiteralFloat(arg.DefaultValue)}, {aliases})";
                    return arg.Parameter.Type.SpecialType == SpecialType.System_Double ? "(double)" + read : read;
                case 2: return $"ReadBool(namedArgs, ctx, {(arg.DefaultValue >= 0.5 ? "true" : "false")}, {aliases})";
                case 3: return $"ReadBoolNonZero(namedArgs, ctx, {(Math.Abs(arg.DefaultValue) > double.Epsilon ? "true" : "false")}, {aliases})";
                case 4: return $"ReadEnum(namedArgs, ctx, ({arg.TypeName}){(int)Math.Round(arg.DefaultValue)}, {aliases})";
                default: throw new InvalidOperationException("Unsupported plan action arg kind.");
            }
        }

        private static string BuildAliases(IReadOnlyList<string> aliases)
        {
            return string.Join(", ", aliases.Select(alias => "\"" + Escape(alias) + "\""));
        }

        private static string Literal(double value)
        {
            if (double.IsNaN(value)) return "global::System.Double.NaN";
            if (double.IsPositiveInfinity(value)) return "global::System.Double.PositiveInfinity";
            if (double.IsNegativeInfinity(value)) return "global::System.Double.NegativeInfinity";
            return value.ToString("R", CultureInfo.InvariantCulture) + "d";
        }

        private static string LiteralFloat(double value) => ((float)value).ToString("R", CultureInfo.InvariantCulture) + "f";
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private sealed class SchemaModel
        {
            public SchemaModel(INamedTypeSymbol argsType, string schemaName, string actionName, IReadOnlyList<ArgModel> args)
            {
                ArgsType = argsType;
                ArgsTypeName = argsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                SchemaName = schemaName;
                ActionName = actionName;
                Args = args;
                HintName = argsType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat).Replace('.', '_') + ".PlanActionSchema.g.cs";
            }

            public INamedTypeSymbol ArgsType { get; }
            public string ArgsTypeName { get; }
            public string SchemaName { get; }
            public string ActionName { get; }
            public IReadOnlyList<ArgModel> Args { get; }
            public string HintName { get; }
        }

        private sealed class ArgModel
        {
            public ArgModel(IParameterSymbol parameter, int kind, double defaultValue, bool required, IReadOnlyList<string> aliases, string displayName, double min, double max, bool validateEnum)
            {
                Parameter = parameter;
                Kind = kind;
                DefaultValue = defaultValue;
                Required = required;
                Aliases = aliases;
                DisplayName = displayName;
                Min = min;
                Max = max;
                ValidateEnum = validateEnum;
                TypeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public IParameterSymbol Parameter { get; }
            public int Kind { get; }
            public double DefaultValue { get; }
            public bool Required { get; }
            public IReadOnlyList<string> Aliases { get; }
            public string DisplayName { get; }
            public double Min { get; }
            public double Max { get; }
            public bool ValidateEnum { get; }
            public string TypeName { get; }
        }

        private sealed class Receiver : ISyntaxContextReceiver
        {
            public List<INamedTypeSymbol> Types { get; } = new List<INamedTypeSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is StructDeclarationSyntax declaration && declaration.AttributeLists.Count > 0 &&
                    context.SemanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol type)
                {
                    Types.Add(type);
                }
            }
        }
    }
}

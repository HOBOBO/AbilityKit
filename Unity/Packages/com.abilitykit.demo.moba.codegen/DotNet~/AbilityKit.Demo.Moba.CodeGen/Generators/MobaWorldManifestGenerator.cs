using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [Generator]
    public sealed class MobaWorldManifestGenerator : ISourceGenerator
    {
        private const string ServiceAttributeName = "AbilityKit.Ability.World.Services.Attributes.WorldServiceAttribute";
        private const string SystemAttributeName = "AbilityKit.Ability.World.WorldSystemAttribute";
        private const string EntitasSystemName = "Entitas.ISystem";
        private const string ContextsName = "Entitas.IContexts";
        private const string ResolverName = "AbilityKit.Ability.World.DI.IWorldResolver";
        private const string ServiceManifestName = "AbilityKit.Demo.Moba.Systems.MobaGeneratedWorldServiceManifest";
        private const string SystemManifestName = "AbilityKit.Demo.Moba.Systems.MobaGeneratedWorldSystemManifest";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new Receiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is Receiver receiver)) return;

            var serviceAttribute = context.Compilation.GetTypeByMetadataName(ServiceAttributeName);
            var systemAttribute = context.Compilation.GetTypeByMetadataName(SystemAttributeName);
            var serviceManifest = context.Compilation.GetTypeByMetadataName(ServiceManifestName);
            var systemManifest = context.Compilation.GetTypeByMetadataName(SystemManifestName);
            if (serviceAttribute != null && serviceManifest != null && IsDeclaredInSource(serviceManifest))
            {
                GenerateServices(context, receiver.Types, serviceAttribute);
            }

            if (systemAttribute != null && systemManifest != null && IsDeclaredInSource(systemManifest))
            {
                GenerateSystems(context, receiver.Types, systemAttribute);
            }
        }

        private static void GenerateServices(
            GeneratorExecutionContext context,
            IReadOnlyList<INamedTypeSymbol> candidates,
            INamedTypeSymbol attributeType)
        {
            var registrations = new List<ServiceRegistration>();
            foreach (var implementation in Distinct(candidates))
            {
                if (!IsMobaServiceNamespace(implementation.ContainingNamespace?.ToDisplayString())) continue;
                foreach (var attribute in implementation.GetAttributes().Where(item =>
                             SymbolEqualityComparer.Default.Equals(item.AttributeClass, attributeType)))
                {
                    if (!TryCreateServiceRegistration(implementation, attribute, out var registration, out var error))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MobaDiagnosticRules.InvalidWorldServiceRule,
                            implementation.Locations.FirstOrDefault(),
                            implementation.ToDisplayString(),
                            error));
                        continue;
                    }

                    registrations.Add(registration);
                }
            }

            ValidateServiceRegistrations(context, registrations);
            registrations.Sort(ServiceRegistration.Compare);
            context.AddSource("MobaGeneratedWorldServiceManifest.g.cs", BuildServiceSource(registrations));
        }

        private static bool TryCreateServiceRegistration(
            INamedTypeSymbol implementation,
            AttributeData attribute,
            out ServiceRegistration registration,
            out string error)
        {
            registration = null!;
            error = null!;
            if (implementation.IsAbstract || implementation.IsGenericType || implementation.ContainingType != null)
            {
                error = "implementation must be a concrete, non-generic top-level type";
                return false;
            }

            if (attribute.ConstructorArguments.Length < 1 ||
                !(attribute.ConstructorArguments[0].Value is INamedTypeSymbol serviceType))
            {
                error = "service contract could not be resolved";
                return false;
            }

            if (!IsAssignableTo(implementation, serviceType))
            {
                error = $"implementation is not assignable to '{serviceType.ToDisplayString()}'";
                return false;
            }

            var lifetime = ReadInt(attribute, 1, 0);
            var isDefault = ReadBool(attribute, 2, true);
            var profile = ReadInt(attribute, 3, -1);
            registration = new ServiceRegistration(serviceType, implementation, lifetime, isDefault, profile);
            return true;
        }

        private static void ValidateServiceRegistrations(
            GeneratorExecutionContext context,
            IReadOnlyList<ServiceRegistration> registrations)
        {
            foreach (var group in registrations.GroupBy(item => item.Implementation, SymbolEqualityComparer.Default))
            {
                var items = group.ToArray();
                for (var i = 0; i < items.Length; i++)
                for (var j = i + 1; j < items.Length; j++)
                {
                    if (ProfilesOverlap(items[i].Profile, items[j].Profile) && items[i].Lifetime != items[j].Lifetime)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MobaDiagnosticRules.ConflictingWorldServiceLifetimeRule,
                            items[j].Implementation.Locations.FirstOrDefault(),
                            items[j].Implementation.ToDisplayString()));
                    }
                }
            }

            foreach (var group in registrations.Where(item => item.IsDefault)
                         .GroupBy(item => item.Service, SymbolEqualityComparer.Default))
            {
                var items = group.ToArray();
                for (var i = 0; i < items.Length; i++)
                for (var j = i + 1; j < items.Length; j++)
                {
                    if (!SymbolEqualityComparer.Default.Equals(items[i].Implementation, items[j].Implementation) &&
                        ProfilesOverlap(items[i].Profile, items[j].Profile))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MobaDiagnosticRules.DuplicateWorldServiceBindingRule,
                            items[j].Implementation.Locations.FirstOrDefault(),
                            group.Key!.ToDisplayString(),
                            items[i].Implementation.ToDisplayString() + ", " + items[j].Implementation.ToDisplayString()));
                    }
                }
            }
        }

        private static string BuildServiceSource(IReadOnlyList<ServiceRegistration> registrations)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("namespace AbilityKit.Demo.Moba.Systems");
            source.AppendLine("{");
            source.AppendLine("    internal static partial class MobaGeneratedWorldServiceManifest");
            source.AppendLine("    {");
            source.AppendLine("        static partial void AddGenerated(global::AbilityKit.Ability.World.DI.WorldContainerBuilder builder, global::AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile profile)");
            source.AppendLine("        {");

            foreach (var group in registrations.GroupBy(item => item.Implementation, SymbolEqualityComparer.Default))
            {
                var items = group.ToArray();
                var implementation = items[0].ImplementationName;
                foreach (var lifetimeGroup in items.GroupBy(item => item.Lifetime))
                {
                    var profile = lifetimeGroup.Aggregate(0, (value, item) => value | item.Profile);
                    source.Append("            if ((((int)profile) & ").Append(profile).AppendLine(") != 0)");
                    source.AppendLine("            {");
                    source.Append("                builder.TryRegisterType(typeof(").Append(implementation)
                        .Append("), typeof(").Append(implementation).Append("), (global::AbilityKit.Ability.World.DI.WorldLifetime)")
                        .Append(lifetimeGroup.Key).AppendLine(");");
                    foreach (var item in lifetimeGroup.Where(item => !SymbolEqualityComparer.Default.Equals(item.Service, item.Implementation)))
                    {
                        source.Append("                builder.TryRegister(typeof(").Append(item.ServiceName)
                            .Append("), typeof(").Append(implementation)
                            .Append("), (global::AbilityKit.Ability.World.DI.WorldLifetime)").Append(item.Lifetime)
                            .Append(", resolver => resolver.Resolve(typeof(").Append(implementation).AppendLine(")));");
                    }
                    source.AppendLine("            }");
                }
            }

            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        private static void GenerateSystems(
            GeneratorExecutionContext context,
            IReadOnlyList<INamedTypeSymbol> candidates,
            INamedTypeSymbol attributeType)
        {
            var entitasSystem = context.Compilation.GetTypeByMetadataName(EntitasSystemName);
            var contexts = context.Compilation.GetTypeByMetadataName(ContextsName);
            var resolver = context.Compilation.GetTypeByMetadataName(ResolverName);
            if (entitasSystem == null || contexts == null || resolver == null) return;

            var systems = new List<SystemRegistration>();
            foreach (var type in Distinct(candidates))
            {
                var attribute = type.GetAttributes().FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.AttributeClass, attributeType));
                if (attribute == null) continue;

                string? error = null;
                if (type.IsAbstract || type.IsGenericType || type.ContainingType != null)
                    error = "system must be a concrete, non-generic top-level type";
                else if (!IsAssignableTo(type, entitasSystem))
                    error = "system must implement Entitas.ISystem";
                else if (!HasSystemConstructor(type, contexts, resolver))
                    error = "system must declare a constructor (Entitas.IContexts, IWorldResolver)";

                if (error != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidWorldSystemRule,
                        type.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        error));
                    continue;
                }

                var order = ReadInt(attribute, 0, 0);
                var phase = 1;
                foreach (var pair in attribute.NamedArguments)
                {
                    if (pair.Key == "Phase" && pair.Value.Value is int value) phase = value;
                }
                systems.Add(new SystemRegistration(type, phase, order));
            }

            foreach (var group in systems.GroupBy(item => (item.Phase, item.Order)).Where(item => item.Count() > 1))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateWorldSystemOrderRule,
                    group.First().Type.Locations.FirstOrDefault(),
                    group.Key.Phase,
                    group.Key.Order,
                    string.Join(", ", group.Select(item => item.Type.ToDisplayString()).OrderBy(item => item, StringComparer.Ordinal))));
            }

            systems.Sort(SystemRegistration.Compare);
            context.AddSource("MobaGeneratedWorldSystemManifest.g.cs", BuildSystemSource(systems));
        }

        private static string BuildSystemSource(IReadOnlyList<SystemRegistration> systems)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("namespace AbilityKit.Demo.Moba.Systems");
            source.AppendLine("{");
            source.AppendLine("    internal static partial class MobaGeneratedWorldSystemManifest");
            source.AppendLine("    {");
            source.AppendLine("        static partial void AddGenerated(global::System.Collections.Generic.List<MobaGeneratedWorldSystemDescriptor> descriptors)");
            source.AppendLine("        {");
            foreach (var system in systems)
            {
                source.Append("            descriptors.Add(new MobaGeneratedWorldSystemDescriptor((global::AbilityKit.Ability.World.WorldSystemPhase)")
                    .Append(system.Phase).Append(", ").Append(system.Order)
                    .Append(", \"").Append(Escape(system.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))).Append("\", ")
                    .Append("(contexts, services) => new ").Append(system.TypeName).AppendLine("(contexts, services)));");
            }
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        private static bool HasSystemConstructor(INamedTypeSymbol type, ITypeSymbol contexts, ITypeSymbol resolver)
        {
            return type.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility != Accessibility.Private &&
                ctor.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, contexts) &&
                SymbolEqualityComparer.Default.Equals(ctor.Parameters[1].Type, resolver));
        }

        private static IEnumerable<INamedTypeSymbol> Distinct(IEnumerable<INamedTypeSymbol> types)
        {
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in types)
            {
                if (seen.Add(type)) yield return type;
            }
        }

        private static bool IsDeclaredInSource(INamedTypeSymbol type) => type.Locations.Any(location => location.IsInSource);

        private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol target)
        {
            if (SymbolEqualityComparer.Default.Equals(type, target)) return true;
            if (type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, target))) return true;
            for (var current = type.BaseType; current != null; current = current.BaseType)
                if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
            return false;
        }

        private static int ReadInt(AttributeData attribute, int index, int fallback)
        {
            return attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is int value
                ? value
                : fallback;
        }

        private static bool ReadBool(AttributeData attribute, int index, bool fallback)
        {
            return attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is bool value
                ? value
                : fallback;
        }

        private static bool ProfilesOverlap(int left, int right) => (left & right) != 0;
        private static bool IsMobaServiceNamespace(string? value)
        {
            if (value == null || value.Length == 0) return false;
            return value.StartsWith("AbilityKit.Demo.Moba.Services", StringComparison.Ordinal) ||
                   value.StartsWith("AbilityKit.Demo.Moba.Runtime.Application.Services", StringComparison.Ordinal) ||
                   value.StartsWith("AbilityKit.Demo.Moba.Gameplay", StringComparison.Ordinal) ||
                   value.StartsWith("AbilityKit.Demo.Moba.Systems", StringComparison.Ordinal) ||
                   value.StartsWith("AbilityKit.Demo.Moba.Runtime.Application.Systems", StringComparison.Ordinal) ||
                   value.StartsWith("AbilityKit.Demo.Moba.Util", StringComparison.Ordinal);
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private sealed class ServiceRegistration
        {
            public ServiceRegistration(INamedTypeSymbol service, INamedTypeSymbol implementation, int lifetime, bool isDefault, int profile)
            {
                Service = service;
                Implementation = implementation;
                Lifetime = lifetime;
                IsDefault = isDefault;
                Profile = profile;
                ServiceName = service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                ImplementationName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol Service { get; }
            public INamedTypeSymbol Implementation { get; }
            public int Lifetime { get; }
            public bool IsDefault { get; }
            public int Profile { get; }
            public string ServiceName { get; }
            public string ImplementationName { get; }

            public static int Compare(ServiceRegistration left, ServiceRegistration right)
            {
                var result = string.CompareOrdinal(left.ImplementationName, right.ImplementationName);
                if (result != 0) return result;
                result = left.Lifetime.CompareTo(right.Lifetime);
                if (result != 0) return result;
                if (left.IsDefault != right.IsDefault) return left.IsDefault ? -1 : 1;
                return string.CompareOrdinal(left.ServiceName, right.ServiceName);
            }
        }

        private sealed class SystemRegistration
        {
            public SystemRegistration(INamedTypeSymbol type, int phase, int order)
            {
                Type = type;
                TypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                Phase = phase;
                Order = order;
            }

            public INamedTypeSymbol Type { get; }
            public string TypeName { get; }
            public int Phase { get; }
            public int Order { get; }

            public static int Compare(SystemRegistration left, SystemRegistration right)
            {
                var result = left.Phase.CompareTo(right.Phase);
                if (result != 0) return result;
                result = left.Order.CompareTo(right.Order);
                return result != 0 ? result : string.CompareOrdinal(left.TypeName, right.TypeName);
            }
        }

        private sealed class Receiver : ISyntaxContextReceiver
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
}

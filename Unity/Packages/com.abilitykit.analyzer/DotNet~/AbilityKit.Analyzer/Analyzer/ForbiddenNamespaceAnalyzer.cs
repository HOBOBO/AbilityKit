using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using AbilityKit.Analyzer.Config;

namespace AbilityKit.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ForbiddenNamespaceAnalyzer : DiagnosticAnalyzer
    {
        private const string ConfigFileName = "PackageConstraints.json";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DiagnosticRules.ForbiddenNamespaceRule,
                DiagnosticRules.ForbiddenAssemblyRule,
                DiagnosticRules.UnmatchedConstraintPackageRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
            context.RegisterCompilationAction(OnCompilation);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var configFile = FindConfigFile(context.Options.AdditionalFiles);
            var config = LoadConfig(configFile, context.CancellationToken);
            if (config == null)
            {
                return;
            }

            var assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            var constraint = config.GetEffectiveConstraint(assemblyName);
            if (constraint != null && constraint.IsEnabled)
            {
                RegisterNamespaceAnalysis(context, assemblyName, constraint);
            }
        }

        private static void OnCompilation(CompilationAnalysisContext context)
        {
            var configFile = FindConfigFile(context.Options.AdditionalFiles);
            var config = LoadConfig(configFile, context.CancellationToken);
            if (config == null)
            {
                return;
            }

            var assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            var constraint = config.GetEffectiveConstraint(assemblyName);
            if (constraint != null && constraint.IsEnabled)
            {
                ReportForbiddenAssemblyReferences(context, assemblyName, constraint);
            }

            ReportUnmatchedConstraints(context, config);
        }

        private static void RegisterNamespaceAnalysis(
            CompilationStartAnalysisContext context,
            string assemblyName,
            PackageConstraint constraint)
        {
            var forbiddenNamespaces = new HashSet<string>(
                constraint.ForbiddenNamespaces ?? new List<string>(),
                StringComparer.Ordinal);
            if (forbiddenNamespaces.Count == 0)
            {
                return;
            }

            context.RegisterSyntaxTreeAction(treeContext =>
            {
                if (IsExcludedPath(treeContext.Tree.FilePath))
                {
                    return;
                }

                var root = treeContext.Tree.GetRoot(treeContext.CancellationToken);
                foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (!constraint.CheckUsingAliases && directive.Alias != null)
                    {
                        continue;
                    }

                    var name = directive.Name?.ToString();
                    if (!IsForbiddenNamespace(name, forbiddenNamespaces))
                    {
                        continue;
                    }

                    treeContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticRules.ForbiddenNamespaceRule,
                        directive.Name.GetLocation(),
                        name,
                        assemblyName));
                }
            });
        }

        private static void ReportForbiddenAssemblyReferences(
            CompilationAnalysisContext context,
            string assemblyName,
            PackageConstraint constraint)
        {
            foreach (var reference in context.Compilation.ReferencedAssemblyNames)
            {
                if (!constraint.IsAssemblyForbidden(reference.Name))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticRules.ForbiddenAssemblyRule,
                    Location.None,
                    reference.Name,
                    assemblyName));
            }
        }

        private static void ReportUnmatchedConstraints(
            CompilationAnalysisContext context,
            PackageConstraintsConfig config)
        {
            var knownAssemblies = ReadKnownAssemblyNames(
                context.Options.AdditionalFiles,
                context.CancellationToken);
            if (knownAssemblies.Count == 0 || config.Constraints == null || config.Constraints.Count == 0)
            {
                return;
            }

            var unmatched = config.Constraints.Keys
                .Where(key => !MatchesAnyAssembly(key, knownAssemblies))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            if (unmatched.Length == 0)
            {
                return;
            }

            foreach (var packageName in unmatched)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticRules.UnmatchedConstraintPackageRule,
                    Location.None,
                    packageName));
            }
        }

        private static AdditionalText FindConfigFile(ImmutableArray<AdditionalText> additionalFiles)
        {
            return additionalFiles.FirstOrDefault(file =>
                file.Path.EndsWith(ConfigFileName, StringComparison.OrdinalIgnoreCase));
        }

        private static PackageConstraintsConfig LoadConfig(
            AdditionalText configFile,
            System.Threading.CancellationToken cancellationToken)
        {
            if (configFile == null)
            {
                return null;
            }

            var text = configFile.GetText(cancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return ConstraintJson.DeserializeConfig(text);
            }
            catch (System.Runtime.Serialization.SerializationException)
            {
                return null;
            }
        }

        private static HashSet<string> ReadKnownAssemblyNames(
            ImmutableArray<AdditionalText> additionalFiles,
            System.Threading.CancellationToken cancellationToken)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in additionalFiles)
            {
                if (!file.Path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = file.GetText(cancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                try
                {
                    var name = ConstraintJson.DeserializeAssemblyName(text);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
                catch (System.Runtime.Serialization.SerializationException)
                {
                    // Invalid asmdef files are reported by Unity and are outside this analyzer's contract.
                }
            }

            return names;
        }

        private static bool MatchesAnyAssembly(string constraintName, HashSet<string> assemblyNames)
        {
            if (string.IsNullOrWhiteSpace(constraintName))
            {
                return false;
            }

            if (!constraintName.EndsWith(".*", StringComparison.Ordinal))
            {
                return assemblyNames.Contains(constraintName);
            }

            var prefix = constraintName.Substring(0, constraintName.Length - 1);
            return assemblyNames.Any(name => name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static bool IsExcludedPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var normalized = filePath.Replace('\\', '/');
            return normalized.Contains("/Example/") ||
                   normalized.Contains("/Examples/") ||
                   normalized.Contains("/Tests/") ||
                   normalized.Contains("/Test/");
        }

        private static bool IsForbiddenNamespace(
            string namespaceName,
            HashSet<string> forbiddenNamespaces)
        {
            if (string.IsNullOrEmpty(namespaceName))
            {
                return false;
            }

            foreach (var forbidden in forbiddenNamespaces)
            {
                if (namespaceName == forbidden ||
                    namespaceName.StartsWith(forbidden + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

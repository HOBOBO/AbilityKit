#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>行为树从授权定义到可运行定义的统一构建结果。</summary>
    public sealed class BehaviorTreeBuildResult
    {
        /// <summary>保留子树引用、可直接序列化的源定义副本。</summary>
        public TreeDefinition SourceDefinition { get; }
        /// <summary>完成子树展开且通过校验的运行时定义；构建失败时为空。</summary>
        public TreeDefinition? CompiledDefinition { get; }
        /// <summary>包含节点来源映射的子树展开结果；源定义不含子树时为空。</summary>
        public ExpansionResult? Expansion { get; }
        /// <summary>构建各阶段产生的结构化诊断。</summary>
        public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }
        /// <summary>是否已得到可创建的运行时定义。</summary>
        public bool Success { get; }

        internal BehaviorTreeBuildResult(
            TreeDefinition sourceDefinition,
            TreeDefinition? compiledDefinition,
            ExpansionResult? expansion,
            IReadOnlyList<ValidationDiagnostic> diagnostics)
        {
            SourceDefinition = sourceDefinition;
            CompiledDefinition = compiledDefinition;
            Expansion = expansion;
            Diagnostics = diagnostics;
            Success = compiledDefinition != null && !ContainsErrors(diagnostics);
        }

        private static bool ContainsErrors(IReadOnlyList<ValidationDiagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == ValidationSeverity.Error) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 统一执行基础校验、跨树展开、展开后校验和运行时可创建性检查。
    /// SourceDefinition 保留子树引用用于序列化，CompiledDefinition 用于执行与预览。
    /// </summary>
    public static class BehaviorTreeBuildPipeline
    {
        /// <summary>存在子树引用但调用方未提供解析器。</summary>
        public const string MissingResolverCode = "BT0800";
        /// <summary>子树缺失、循环引用或黑板绑定冲突导致展开失败。</summary>
        public const string ExpansionFailedCode = "BT0801";
        /// <summary>节点工厂或节点初始化无法创建运行时实例。</summary>
        public const string RuntimeCreationFailedCode = "BT0900";

        /// <summary>构建授权文档并验证其能否创建运行时。</summary>
        public static BehaviorTreeBuildResult Build(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            TreeDefinitionResolver? resolver = null,
            bool verifyRuntimeCreation = true)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            return Build(TreeExporter.ToRuntimeDefinition(document), registry, resolver, verifyRuntimeCreation);
        }

        /// <summary>构建运行时定义并验证其能否创建运行时。</summary>
        public static BehaviorTreeBuildResult Build(
            TreeDefinition definition,
            NodeRegistry registry,
            TreeDefinitionResolver? resolver = null,
            bool verifyRuntimeCreation = true)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var source = definition.DeepClone();
            var diagnostics = new List<ValidationDiagnostic>();
            AddDistinct(diagnostics, TreeValidator.ValidateDiagnostics(source, registry));
            if (HasErrors(diagnostics))
                return new BehaviorTreeBuildResult(source, null, null, diagnostics);

            TreeDefinition compiled = source.DeepClone();
            ExpansionResult? expansion = null;
            if (ContainsSubtree(source))
            {
                if (resolver == null)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        MissingResolverCode,
                        ValidationSeverity.Error,
                        "行为树包含子树引用，但当前构建入口没有提供子树解析上下文。",
                        FindFirstSubtreeNodeId(source)));
                    return new BehaviorTreeBuildResult(source, null, null, diagnostics);
                }

                try
                {
                    expansion = TreeCompiler.ExpandReferences(source, resolver, registry);
                    compiled = expansion.Definition;
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        ExpansionFailedCode,
                        ValidationSeverity.Error,
                        "子树展开失败：" + (ex is SubtreeExpansionException
                            ? ex.Message : ReadExceptionMessage(ex)),
                        ex is SubtreeExpansionException subtreeError
                            && string.Equals(subtreeError.SourceTreeId, source.TreeId, StringComparison.Ordinal)
                            ? subtreeError.ReferenceNodeId
                            : ResolveMentionedNodeId(ex.Message, source)));
                    return new BehaviorTreeBuildResult(source, null, null, diagnostics);
                }
            }

            AddDistinct(diagnostics, TreeValidator.ValidateDiagnostics(compiled, registry));
            if (HasErrors(diagnostics))
                return new BehaviorTreeBuildResult(source, null, expansion, diagnostics);

            if (verifyRuntimeCreation)
            {
                TreeRuntime? runtime = null;
                try
                {
                    runtime = expansion == null
                        ? TreeRuntime.Create(compiled, registry)
                        : TreeRuntime.Create(expansion, registry);
                }
                catch (Exception ex)
                {
                    var compiledNodeId = ResolveMentionedNodeId(ex.Message, compiled);
                    var sourceNodeId = ResolveSourceNodeId(compiledNodeId, expansion);
                    diagnostics.Add(new ValidationDiagnostic(
                        RuntimeCreationFailedCode,
                        ValidationSeverity.Error,
                        "运行时节点创建失败：" + ReadExceptionMessage(ex),
                        sourceNodeId));
                }
                finally
                {
                    if (runtime != null)
                    {
                        try { runtime.Dispose(); }
                        catch (Exception) { /* 构建校验以首次创建错误为准。 */ }
                    }
                }
            }

            return new BehaviorTreeBuildResult(
                source,
                HasErrors(diagnostics) ? null : compiled,
                expansion,
                diagnostics);
        }

        private static bool ContainsSubtree(TreeDefinition definition)
        {
            foreach (var node in definition.Nodes)
            {
                if (node.Type == BuiltInNodeTypes.Subtree) return true;
            }
            return false;
        }

        private static string? FindFirstSubtreeNodeId(TreeDefinition definition)
        {
            foreach (var node in definition.Nodes)
            {
                if (node.Type == BuiltInNodeTypes.Subtree) return node.Id;
            }
            return null;
        }

        private static string? ResolveMentionedNodeId(string? message, TreeDefinition definition)
        {
            if (string.IsNullOrEmpty(message)) return null;
            string? match = null;
            foreach (var node in definition.Nodes)
            {
                if (!string.IsNullOrEmpty(node.Id)
                    && message!.IndexOf("'" + node.Id + "'", StringComparison.Ordinal) >= 0
                    && (match == null || node.Id.Length > match.Length))
                {
                    match = node.Id;
                }
            }
            return match;
        }

        private static string? ResolveSourceNodeId(string? compiledNodeId, ExpansionResult? expansion)
        {
            if (compiledNodeId == null || expansion == null) return compiledNodeId;
            return expansion.NodeSourceNode.TryGetValue(compiledNodeId, out var sourceNodeId)
                ? sourceNodeId
                : compiledNodeId;
        }

        private static string ReadExceptionMessage(Exception exception)
        {
            var current = exception;
            while (current.InnerException != null) current = current.InnerException;
            return string.IsNullOrWhiteSpace(current.Message)
                ? current.GetType().Name
                : current.Message;
        }

        private static bool HasErrors(IReadOnlyList<ValidationDiagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == ValidationSeverity.Error) return true;
            }
            return false;
        }

        private static void AddDistinct(
            List<ValidationDiagnostic> destination,
            IEnumerable<ValidationDiagnostic> source)
        {
            foreach (var candidate in source)
            {
                var duplicate = false;
                foreach (var existing in destination)
                {
                    if (existing.Code == candidate.Code
                        && existing.Message == candidate.Message
                        && existing.NodeId == candidate.NodeId
                        && existing.PropertyName == candidate.PropertyName
                        && existing.BlackboardKey == candidate.BlackboardKey)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) destination.Add(candidate);
            }
        }
    }
}

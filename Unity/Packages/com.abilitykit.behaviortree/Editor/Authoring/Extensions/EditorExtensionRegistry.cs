#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;

using AbilityKit.BehaviorTree.Editor;
using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Authoring.Extensions
{
    /// <summary>
    /// Behavior Tree 编辑器扩展协议核心：四类确定性优先级 registry——
    /// Inspector 区块 / 属性字段编辑器 / authoring 诊断 / 节点目录源。
    /// 注册返回独立 <see cref="IDisposable"/>；所有产出枚举都对贡献方调用做异常隔离。
    /// 本类只提供「注册 + 确定性、异常安全的产出」，不改动既有窗口 / renderer / catalog，
    /// 由后续接线（P2）消费这些产出接入 UI。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorExtensionRegistry")]
    public static class EditorExtensionRegistry
    {
        private static readonly PriorityRegistration<IInspectorSectionContributor> InspectorSections = new();
        private static readonly PriorityRegistration<IPropertyFieldEditor> PropertyFieldEditors = new();
        private static readonly PriorityRegistration<IAuthoringDiagnosticContributor> DiagnosticContributors = new();
        private static readonly PriorityRegistration<INodeCatalogSource> CatalogSources = new();

        public static IDisposable RegisterInspectorSectionContributor(
            IInspectorSectionContributor contributor,
            int priority = EditorExtensionPriority.Package)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            return InspectorSections.Register(contributor, priority);
        }

        public static IDisposable RegisterPropertyFieldEditor(
            IPropertyFieldEditor editor,
            int priority = EditorExtensionPriority.Package)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            return PropertyFieldEditors.Register(editor, priority);
        }

        public static IDisposable RegisterDiagnosticContributor(
            IAuthoringDiagnosticContributor contributor,
            int priority = EditorExtensionPriority.Package)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            return DiagnosticContributors.Register(contributor, priority);
        }

        public static IDisposable RegisterNodeCatalogSource(
            INodeCatalogSource source,
            int priority = EditorExtensionPriority.Package)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return CatalogSources.Register(source, priority);
        }

        /// <summary>按贡献方优先级枚举所有 Inspector 区块（异常隔离：单个贡献方失败不影响其它）。</summary>
        public static IEnumerable<InspectorSection> EnumerateInspectorSections(InspectorSectionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return ExtensionSafeEnumerate.Enumerate(
                InspectorSections.Enumerate(),
                contributor => contributor.BuildSections(context));
        }

        /// <summary>按贡献方优先级枚举所有字段编辑器绑定（异常隔离）。</summary>
        public static IEnumerable<PropertyFieldEditorBinding> EnumeratePropertyFieldEditorBindings()
        {
            return ExtensionSafeEnumerate.Enumerate(
                PropertyFieldEditors.Enumerate(),
                editor => editor.GetBindings());
        }

        /// <summary>
        /// 解析给定 (节点类型, 字段名) 的字段编辑器绑定；无匹配返回 null。
        /// 优先级高者优先，同优先级先注册者优先，空 <see cref="PropertyFieldEditorBinding.TypeId"/> 匹配任意类型。
        /// </summary>
        public static PropertyFieldEditorBinding? ResolvePropertyFieldEditor(string typeId, string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            typeId ??= "";
            foreach (var binding in EnumeratePropertyFieldEditorBindings())
            {
                if (binding == null) continue;
                if (!string.Equals(binding.FieldName, fieldName, StringComparison.Ordinal)) continue;
                if (binding.TypeId.Length != 0 && !string.Equals(binding.TypeId, typeId, StringComparison.Ordinal)) continue;
                return binding;
            }
            return null;
        }

        /// <summary>
        /// 收集所有 authoring 诊断（异常隔离），按贡献方优先级、贡献方内出现顺序排列。
        /// 与 <see cref="EditorDiagnostics"/> 的运行时结构校验互补。
        /// </summary>
        public static IReadOnlyList<EditorDiagnostic> Analyze(
            AuthoringSourceDocument document,
            NodeRegistry registry)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var context = new AuthoringDiagnosticContext(document, registry);
            var result = new List<EditorDiagnostic>();
            foreach (var diagnostic in ExtensionSafeEnumerate.Enumerate(
                         DiagnosticContributors.Enumerate(),
                         contributor => contributor.Analyze(context)))
            {
                if (diagnostic != null) result.Add(diagnostic);
            }
            return result;
        }

        /// <summary>
        /// 收集所有节点目录描述符（异常隔离）；type id 冲突时按优先级首个出现者胜出，
        /// 其余重复项丢弃。结果顺序即合并后的目录顺序。
        /// </summary>
        public static IReadOnlyList<NodeDescriptor> CollectCatalogDescriptors()
        {
            var result = new List<NodeDescriptor>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var descriptor in ExtensionSafeEnumerate.Enumerate(
                         CatalogSources.Enumerate(),
                         source => source.GetDescriptors()))
            {
                if (descriptor == null || string.IsNullOrEmpty(descriptor.TypeId)) continue;
                if (!seen.Add(descriptor.TypeId)) continue;
                result.Add(descriptor);
            }
            return result;
        }

        /// <summary>清空全部注册（测试与域重载卫生用）。</summary>
        public static void Reset()
        {
            InspectorSections.Reset();
            PropertyFieldEditors.Reset();
            DiagnosticContributors.Reset();
            CatalogSources.Reset();
        }
    }
}

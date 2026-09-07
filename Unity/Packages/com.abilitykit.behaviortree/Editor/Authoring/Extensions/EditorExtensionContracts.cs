#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using UnityEngine.UIElements;

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
    /// 扩展贡献优先级：数值越大越先参与；发生冲突（重复 type id、重复字段绑定）时优先级高者胜出。
    /// 同优先级按注册先后（先注册者在前）。语义与 <see cref="AuthoringDocumentPriority"/> 对齐。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorExtensionPriority")]
    public static class EditorExtensionPriority
    {
        /// <summary>框架内置 / 基础行为。</summary>
        public const int Framework = 0;
        /// <summary>领域包默认扩展。</summary>
        public const int Package = 100;
        /// <summary>项目定制覆盖。</summary>
        public const int Project = 200;
    }

    /// <summary>节点 Inspector 自定义区块的构建上下文。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtInspectorSectionContext")]
    public sealed class InspectorSectionContext
    {
        public InspectorSectionContext(AuthoringSourceDocument document, NodeDefinition node, bool isReadOnly)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            IsReadOnly = isReadOnly;
        }

        /// <summary>当前授权文档。</summary>
        public AuthoringSourceDocument Document { get; }
        /// <summary>当前选中节点。</summary>
        public NodeDefinition Node { get; }
        /// <summary>观察 / 只读模式。</summary>
        public bool IsReadOnly { get; }
    }

    /// <summary>
    /// Inspector 中追加的自定义区块：标题 + 展示排序 + UI 工厂。UI 工厂由渲染器在接线阶段调用，
    /// 本协议核心只负责确定性地产出区块声明，不在此处构造 UI。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtInspectorSection")]
    public sealed class InspectorSection
    {
        public InspectorSection(string title, Func<VisualElement> build, int order = 0)
        {
            Title = string.IsNullOrWhiteSpace(title)
                ? throw new ArgumentException("A section title is required.", nameof(title))
                : title;
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Order = order;
        }

        public string Title { get; }
        /// <summary>渲染器合并多个贡献方区块时的稳定排序键（升序）。</summary>
        public int Order { get; }
        public Func<VisualElement> Build { get; }
    }

    /// <summary>为节点 Inspector 追加自定义区块。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtInspectorSectionContributor")]
    public interface IInspectorSectionContributor
    {
        IEnumerable<InspectorSection> BuildSections(InspectorSectionContext context);
    }

    /// <summary>自定义属性字段编辑器控件的构建上下文。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtPropertyFieldEditorContext")]
    public sealed class PropertyFieldEditorContext
    {
        public PropertyFieldEditorContext(
            NodeDescriptor descriptor,
            PropertyField field,
            PropertyValue currentValue,
            bool isReadOnly,
            Action<PropertyValue> commit)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Field = field ?? throw new ArgumentNullException(nameof(field));
            CurrentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));
            IsReadOnly = isReadOnly;
            Commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        /// <summary>字段所属节点的描述符。</summary>
        public NodeDescriptor Descriptor { get; }
        /// <summary>被编辑的属性字段声明。</summary>
        public PropertyField Field { get; }
        /// <summary>当前值（已套用默认值）。</summary>
        public PropertyValue CurrentValue { get; }
        public bool IsReadOnly { get; }
        /// <summary>把新值写回节点属性。</summary>
        public Action<PropertyValue> Commit { get; }
    }

    /// <summary>
    /// 字段编辑器绑定：声明它处理哪个节点类型 + 字段名，并提供 UI 工厂。
    /// <see cref="TypeId"/> 为空串时匹配任意节点类型；字段名做序数比较。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtPropertyFieldEditorBinding")]
    public sealed class PropertyFieldEditorBinding
    {
        public PropertyFieldEditorBinding(
            string fieldName,
            Func<PropertyFieldEditorContext, VisualElement> createEditor,
            string? typeId = null)
        {
            FieldName = string.IsNullOrWhiteSpace(fieldName)
                ? throw new ArgumentException("A field name is required.", nameof(fieldName))
                : fieldName;
            CreateEditor = createEditor ?? throw new ArgumentNullException(nameof(createEditor));
            TypeId = typeId ?? "";
        }

        /// <summary>匹配的属性 schema 字段名（序数比较）。</summary>
        public string FieldName { get; }
        /// <summary>匹配的节点类型 id；空串表示匹配任意节点类型。</summary>
        public string TypeId { get; }
        public Func<PropertyFieldEditorContext, VisualElement> CreateEditor { get; }
    }

    /// <summary>为特定节点属性字段提供自定义编辑器控件。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtPropertyFieldEditor")]
    public interface IPropertyFieldEditor
    {
        IEnumerable<PropertyFieldEditorBinding> GetBindings();
    }

    /// <summary>authoring 诊断分析的上下文。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringDiagnosticContext")]
    public sealed class AuthoringDiagnosticContext
    {
        public AuthoringDiagnosticContext(AuthoringSourceDocument document, NodeRegistry registry)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public AuthoringSourceDocument Document { get; }
        public NodeRegistry Registry { get; }

        public TreeDefinition Definition => Document.Tree;
    }

    /// <summary>在运行时结构校验之外贡献额外的 authoring 诊断。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtAuthoringDiagnosticContributor")]
    public interface IAuthoringDiagnosticContributor
    {
        IEnumerable<EditorDiagnostic> Analyze(AuthoringDiagnosticContext context);
    }

    /// <summary>
    /// 以编程方式贡献节点描述符，补充 <c>[NodeType]</c> 程序集扫描之外的可程序化来源
    /// （例如由配置 / 内容清单驱动的节点）。产生结果按贡献方优先级合并，type id 冲突时优先级高者胜出。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtNodeCatalogSource")]
    public interface INodeCatalogSource
    {
        IEnumerable<NodeDescriptor> GetDescriptors();
    }
}

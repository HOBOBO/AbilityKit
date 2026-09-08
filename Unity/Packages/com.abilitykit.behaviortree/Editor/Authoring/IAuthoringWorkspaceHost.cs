#nullable enable

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 授权工作台的共享宿主契约：图、属性面板、侧栏视图共同依赖的最小只读上下文与变更入口。
    /// 图专用能力见 <see cref="IAuthoringGraphHost"/>，属性面板专用能力见 <see cref="IAuthoringInspectorHost"/>，
    /// 两者都继承本契约，故任一视图既可只依赖共享上下文，也可依赖各自的专用能力。
    /// </summary>
    internal interface IAuthoringWorkspaceHost
    {
        AuthoringSourceDocument Document { get; }
        bool IsReadOnly { get; }
        string ResolveNodeDisplayName(NodeDefinition node);
        void RecordChange();
        void RecordChange(string beforeChangeSnapshot);
    }
}

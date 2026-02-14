using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    public interface IExplainContextEditorProvider : IRegistryPriority
    {
        bool CanEdit(in PipelineItemKey key);
        string GetButtonText(in PipelineItemKey key);
        string GetWindowTitle(in PipelineItemKey key);
        UnityEngine.UIElements.VisualElement BuildEditor(ExplainContextEditorContext context);
    }
}

using AbilityKit.Ability.Explain;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    public interface IExplainDetailsSectionProvider
    {
        int Priority { get; }

        bool CanProvide(ExplainNode node, ExplainDetailsContext context);

        void Build(VisualElement container, ExplainNode node, ExplainDetailsContext context);
    }
}

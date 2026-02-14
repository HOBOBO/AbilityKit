using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    public interface IExplainNodeContextMenuProvider : IRegistryPriority
    {
        bool CanProvide(ExplainNode node, ExplainNodeContextMenuContext context);
        void BuildMenu(ExplainNode node, ExplainNodeContextMenuContext context, DropdownMenu menu);
    }
}

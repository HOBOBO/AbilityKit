using System.Collections.Generic;
using AbilityKit.Ability.Explain;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    public interface IExplainEntityListModule : IRegistryPriority
    {
        bool CanHandle(IEntityProvider provider);
        VisualElement BuildFilters(ExplainEntityListModuleContext context);
        List<ExplainEntityListGroup> BuildGroups(IEntityProvider provider, List<PipelineItemKey> entities);
    }
}

using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    public interface IEntityProvider
    {
        IEnumerable<PipelineItemKey> Query(string searchText);
        string GetDisplayName(in PipelineItemKey key);
    }
}

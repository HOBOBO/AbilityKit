using System.Collections.Generic;

namespace Emilia.Kit
{
    public interface IHierarchyAsset
    {
        IHierarchyAsset parent { get; set; }

        IReadOnlyList<IHierarchyAsset> children { get; }

        void AddChild(IHierarchyAsset child);

        void RemoveChild(IHierarchyAsset child);
    }
}
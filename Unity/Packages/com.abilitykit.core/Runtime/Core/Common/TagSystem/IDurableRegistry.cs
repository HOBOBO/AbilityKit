using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface IDurableRegistry
    {
        void Register(IDurable durable);
        bool Unregister(IDurable durable);

        IReadOnlyList<IDurable> GetByOwner(int ownerId);
    }
}

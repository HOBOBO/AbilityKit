using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public interface IAttributeDependencyProvider
    {
        IEnumerable<AttributeId> GetDependencies();
    }
}

using System.Collections.Generic;

namespace AbilityKit.Core.Common.AttributeSystem
{
    public interface IAttributeDependencyProvider
    {
        IEnumerable<AttributeId> GetDependencies();
    }
}

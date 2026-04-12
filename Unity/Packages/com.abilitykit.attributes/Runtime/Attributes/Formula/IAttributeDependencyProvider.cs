using System.Collections.Generic;
using AbilityKit.Attributes.Core;

namespace AbilityKit.Attributes.Formula
{
    public interface IAttributeDependencyProvider
    {
        IEnumerable<AttributeId> GetDependencies();
    }
}

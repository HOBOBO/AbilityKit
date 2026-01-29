using System;

namespace AbilityKit.Ability.Flow
{
    public interface IFlowRootProvider<in TArgs>
    {
        IFlowNode CreateRoot(TArgs args);
    }
}

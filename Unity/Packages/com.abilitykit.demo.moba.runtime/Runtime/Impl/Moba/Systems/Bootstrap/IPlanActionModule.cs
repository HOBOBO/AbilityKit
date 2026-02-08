using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public interface IPlanActionModule
    {
        void Register(ActionRegistry actions, IWorldResolver services);
    }
}

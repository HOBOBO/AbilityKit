using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterWorldModules(WorldContainerBuilder builder)
        {
            builder.AddModule(new EntitasEcsWorldModule());
            builder.AddModule(new TriggeringWorldModule());
        }
    }
}

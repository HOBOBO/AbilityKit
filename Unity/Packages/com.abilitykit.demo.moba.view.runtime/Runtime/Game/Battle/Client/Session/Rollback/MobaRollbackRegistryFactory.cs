using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Rollback;

namespace AbilityKit.Game.Battle
{
    public sealed class MobaRollbackRegistryFactory : IBattleRollbackRegistryFactory
    {
        public RollbackRegistry Create(IWorld world)
        {
            return MobaRollbackRegistryBuilder.Create(world);
        }
    }
}

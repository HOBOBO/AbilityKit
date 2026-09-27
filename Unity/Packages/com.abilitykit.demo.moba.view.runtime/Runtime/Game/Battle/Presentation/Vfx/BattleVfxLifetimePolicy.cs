using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Shared.Time;
using AbilityKit.Game.Flow;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxLifetimePolicy
    {
        private readonly IBattleViewTimeSource _time;

        public BattleVfxLifetimePolicy(IBattleViewTimeSource time = null)
        {
            _time = time ?? UnityBattleViewTimeSource.Shared;
        }

        public void AttachIfNeeded(EC.IEntity entity, int durationMs)
        {
            if (!entity.IsValid) return;
            if (durationMs <= 0) return;

            entity.WithRef(new BattleVfxLifetimeComponent
            {
                ExpireAtTime = PresentationLifetimePolicy.ExpireAt(_time.TimeSeconds, durationMs)
            });
        }

        public bool IsExpired(EC.IEntity entity)
        {
            if (!entity.TryGetRef(out BattleVfxLifetimeComponent life) || life == null) return false;
            return PresentationLifetimePolicy.IsExpired(_time.TimeSeconds, life.ExpireAtTime);
        }
    }
}

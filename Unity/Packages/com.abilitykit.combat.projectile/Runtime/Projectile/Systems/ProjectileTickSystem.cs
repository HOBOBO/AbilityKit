using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Core.Common.Projectile
{
    [WorldSystem(WorldSystemOrder.CoreBase + 2000 + WorldSystemOrder.Normal, Phase = WorldSystemPhase.Execute)]
    public sealed class ProjectileTickSystem : WorldSystemBase
    {
        private readonly IProjectileService _projectiles;
        private readonly IWorldClock _clock;
        private readonly IFrameTime _frameTime;
        private int _fallbackFrame;

        public ProjectileTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
            _projectiles = services.Resolve<IProjectileService>();
            _clock = services.Resolve<IWorldClock>();
            services.TryResolve<IFrameTime>(out _frameTime);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null) return;
            if (_clock == null) return;

            if (_frameTime != null)
            {
                _projectiles.Tick(_frameTime.Frame.Value, _frameTime.DeltaTime);
                return;
            }

            _fallbackFrame++;
            _projectiles.Tick(_fallbackFrame, _clock.DeltaTime);
        }
    }
}

using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaProjectileLinkService _links;
        private MobaActorRegistry _registry;

        private readonly List<ProjectileTickEvent> _ticks = new List<ProjectileTickEvent>(128);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(64);

        public MobaProjectileSyncSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _projectiles);
            Services.TryGet(out _links);
            Services.TryGet(out _registry);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _links == null || _registry == null) return;

            _ticks.Clear();
            _projectiles.DrainTickEvents(_ticks);
            for (int i = 0; i < _ticks.Count; i++)
            {
                var evt = _ticks[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (!_registry.TryGet(actorId, out var e) || e == null) continue;
                if (!e.hasTransform) continue;

                var t = e.transform.Value;
                var nt = new Transform3(evt.Position, t.Rotation, t.Scale);
                e.ReplaceTransform(nt);
            }

            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (_registry.TryGet(actorId, out var e) && e != null)
                {
                    try { e.Destroy(); }
                    catch { }
                }

                _registry.Unregister(actorId);
                _links.UnlinkByProjectileId(evt.Projectile);
            }
        }
    }
}

using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.View.Abstractions.Shared.Types;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;
using UnityEngine;
using AbstractBattleProjectileTriggerHitInput = AbilityKit.Demo.Moba.View.Abstractions.Battle.View.BattleProjectileTriggerHitInput;
using AbstractBattleProjectileVfxIds = AbilityKit.Demo.Moba.View.Abstractions.Battle.View.BattleProjectileVfxIds;
using AbstractBattleProjectileVfxResolver = AbilityKit.Demo.Moba.View.Abstractions.Battle.View.BattleProjectileVfxResolver;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileVfxResolver
    {
        private readonly BattleViewResourceProvider _resources;
        private readonly BattleProjectileTriggerHitInputResolver _triggerInputs;
        private readonly BattleProjectileSnapshotVfxIdResolver _snapshotVfxIds;
        private readonly AbstractBattleProjectileVfxResolver _resolver;

        public BattleProjectileVfxResolver(
            BattleViewResourceProvider resources = null,
            BattleProjectileTriggerHitInputResolver triggerInputs = null,
            BattleProjectileSnapshotVfxIdResolver snapshotVfxIds = null,
            AbstractBattleProjectileVfxResolver resolver = null)
        {
            _resources = BattleViewResourceProvider.OrDefault(resources);
            _triggerInputs = triggerInputs ?? new BattleProjectileTriggerHitInputResolver();
            _resolver = resolver ?? new AbstractBattleProjectileVfxResolver();
            _snapshotVfxIds = snapshotVfxIds ?? new BattleProjectileSnapshotVfxIdResolver(_resolver);
        }

        public bool TryResolveTriggerHit(in TriggerEvent evt, out int vfxId, out Vector3 position, out int projectileId)
        {
            vfxId = 0;
            position = default;
            projectileId = 0;

            if (!_triggerInputs.TryResolve(in evt, out var input)) return false;

            var projectile = _resources.TryGetProjectile(input.TemplateId);
            if (projectile == null) return false;

            projectileId = input.ProjectileId;

            var abstractInput = new AbstractBattleProjectileTriggerHitInput(input.TemplateId, ToMobaFloat3(input.HitPoint));
            var vfxIds = ToVfxIds(projectile);
            if (!_resolver.TryResolveTriggerHit(in abstractInput, in vfxIds, out var abstractSpec)) return false;

            vfxId = abstractSpec.VfxId > 0 ? abstractSpec.VfxId : BattleViewPlaceholderIds.ProjectileHitVfx;
            position = ToUnityVector3(abstractSpec.Position);
            return true;
        }

        public int ResolveSnapshotVfxId(int templateId, int kind)
        {
            if (templateId <= 0) return 0;

            var projectile = _resources.TryGetProjectile(templateId);
            var vfxId = _snapshotVfxIds.Resolve(projectile, kind);
            if (vfxId > 0) return vfxId;

            return kind == (int)ProjectileEventKind.Spawn
                ? BattleViewPlaceholderIds.ProjectileSpawnVfx
                : 0;
        }

        public Vector3 ResolveSnapshotPosition(float x, float y, float z)
        {
            return ToUnityVector3(_resolver.ResolveSnapshotPosition(x, y, z));
        }

        private static AbstractBattleProjectileVfxIds ToVfxIds(ProjectileMO projectile)
        {
            if (projectile == null) return default;
            return new AbstractBattleProjectileVfxIds(
                projectile.VfxId,
                projectile.OnSpawnVfxId,
                projectile.OnHitVfxId,
                projectile.OnExpireVfxId);
        }

        private static MobaFloat3 ToMobaFloat3(Vector3 value)
        {
            return new MobaFloat3(value.x, value.y, value.z);
        }

        private static Vector3 ToUnityVector3(MobaFloat3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }

    internal readonly struct BattleProjectileTriggerHitInput
    {
        public BattleProjectileTriggerHitInput(int templateId, in Vector3 hitPoint, int projectileId = 0)
        {
            TemplateId = templateId;
            HitPoint = hitPoint;
            ProjectileId = projectileId;
        }

        public int TemplateId { get; }
        public Vector3 HitPoint { get; }
        /// <summary>
        /// Used for cross-source deduplication between Trigger and Snapshot paths.
        /// </summary>
        public int ProjectileId { get; }
    }

    internal sealed class BattleProjectileTriggerHitInputResolver
    {
        public bool TryResolve(in TriggerEvent evt, out BattleProjectileTriggerHitInput input)
        {
            input = default;
            if (evt.Args == null) return false;

            if (!evt.Args.TryGetValue(ProjectileTriggering.Args.TemplateId, out var templateObj)
                || templateObj is not int templateId
                || templateId <= 0)
            {
                return false;
            }

            if (!evt.Args.TryGetValue(ProjectileTriggering.Args.HitPoint, out var hitPointObj)
                || hitPointObj is not Vec3 hitPoint)
            {
                return false;
            }

            var position = new Vector3(hitPoint.X, hitPoint.Y, hitPoint.Z);

            evt.Args.TryGetValue(ProjectileTriggering.Args.ProjectileId, out var projectileIdObj);
            var projectileId = projectileIdObj is int pid ? pid : 0;

            input = new BattleProjectileTriggerHitInput(templateId, in position, projectileId);
            return true;
        }
    }

    internal sealed class BattleProjectileSnapshotVfxIdResolver
    {
        private readonly AbstractBattleProjectileVfxResolver _resolver;

        public BattleProjectileSnapshotVfxIdResolver(AbstractBattleProjectileVfxResolver resolver = null)
        {
            _resolver = resolver ?? new AbstractBattleProjectileVfxResolver();
        }

        public int Resolve(ProjectileMO projectile, int kind)
        {
            if (projectile == null) return 0;

            var vfxIds = new AbstractBattleProjectileVfxIds(
                projectile.VfxId,
                projectile.OnSpawnVfxId,
                projectile.OnHitVfxId,
                projectile.OnExpireVfxId);
            return _resolver.ResolveSnapshotVfxId(in vfxIds, kind);
        }
    }
}

using AbilityKit.Ability.Impl.Moba.Effects.Model;
using AbilityKit.Ability.Impl.Moba.Effects.Runtime.Snapshots;
using AbilityKit.Effects.Core;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime
{
    internal static class MobaEffectSnapshotBuilder
    {
        public static MobaLauncherEffectSnapshot BuildLauncherSnapshot(EffectRegistry registry, in MobaEffectQueryContext ctx)
        {
            var snapshot = MobaLauncherEffectSnapshot.Default;
            if (registry == null) return snapshot;

            ApplyLauncherScope(registry, ref snapshot, MobaEffectScopeKeys.Global());
            if (ctx.ActorId != 0) ApplyLauncherScope(registry, ref snapshot, MobaEffectScopeKeys.Unit(ctx.ActorId));
            if (ctx.SkillId != 0) ApplyLauncherScope(registry, ref snapshot, MobaEffectScopeKeys.SkillId(ctx.SkillId));
            if (ctx.LauncherId != 0) ApplyLauncherScope(registry, ref snapshot, MobaEffectScopeKeys.LauncherId(ctx.LauncherId));

            return snapshot;
        }

        public static MobaProjectileEffectSnapshot BuildProjectileSnapshot(EffectRegistry registry, in MobaEffectQueryContext ctx)
        {
            var snapshot = MobaProjectileEffectSnapshot.Default;
            if (registry == null) return snapshot;

            ApplyProjectileScope(registry, ref snapshot, MobaEffectScopeKeys.Global());
            if (ctx.ActorId != 0) ApplyProjectileScope(registry, ref snapshot, MobaEffectScopeKeys.Unit(ctx.ActorId));
            if (ctx.SkillId != 0) ApplyProjectileScope(registry, ref snapshot, MobaEffectScopeKeys.SkillId(ctx.SkillId));
            if (ctx.LauncherId != 0) ApplyProjectileScope(registry, ref snapshot, MobaEffectScopeKeys.LauncherId(ctx.LauncherId));
            if (ctx.ProjectileId != 0) ApplyProjectileScope(registry, ref snapshot, MobaEffectScopeKeys.ProjectileId(ctx.ProjectileId));

            return snapshot;
        }

        private static void ApplyLauncherScope(EffectRegistry registry, ref MobaLauncherEffectSnapshot snapshot, in EffectScopeKey scope)
        {
            var list = registry.GetInstances(in scope);
            for (var i = 0; i < list.Count; i++)
            {
                ApplyLauncherInstance(ref snapshot, list[i]);
            }
        }

        private static void ApplyProjectileScope(EffectRegistry registry, ref MobaProjectileEffectSnapshot snapshot, in EffectScopeKey scope)
        {
            var list = registry.GetInstances(in scope);
            for (var i = 0; i < list.Count; i++)
            {
                ApplyProjectileInstance(ref snapshot, list[i]);
            }
        }

        private static void ApplyLauncherInstance(ref MobaLauncherEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (var i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                switch ((MobaEffectStatKey)s.KeyId)
                {
                    case MobaEffectStatKey.LauncherIntervalBaseAddFrames:
                        if (s.Op == EffectOp.Add) snapshot.IntervalBaseAddFrames += s.Value.I;
                        break;

                    case MobaEffectStatKey.LauncherIntervalPostMul:
                        if (s.Op == EffectOp.Mul) snapshot.IntervalPostMul *= s.Value.F;
                        break;

                    case MobaEffectStatKey.LauncherExtraProjectilesPerShot:
                        if (s.Op == EffectOp.Add) snapshot.ExtraProjectilesPerShot += s.Value.I;
                        break;

                    case MobaEffectStatKey.LauncherExtraTotalCount:
                        if (s.Op == EffectOp.Add) snapshot.ExtraTotalCount += s.Value.I;
                        break;
                }
            }
        }

        private static void ApplyProjectileInstance(ref MobaProjectileEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (var i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                switch ((MobaEffectStatKey)s.KeyId)
                {
                    case MobaEffectStatKey.ProjectileDamageMul:
                        if (s.Op == EffectOp.Mul) snapshot.DamageMul *= s.Value.F;
                        break;

                    case MobaEffectStatKey.ProjectileSpeedMul:
                        if (s.Op == EffectOp.Mul) snapshot.SpeedMul *= s.Value.F;
                        break;

                    case MobaEffectStatKey.ProjectilePierce:
                        if (s.Op == EffectOp.Add) snapshot.Pierce += s.Value.I;
                        break;
                }
            }
        }
    }
}

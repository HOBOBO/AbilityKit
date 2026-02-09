using System;
using System.Collections.Generic;
using AbilityKit.Effects.Core.Snapshots;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core
{
    public sealed class EffectRegistry
    {
        private readonly Dictionary<EffectScopeKey, List<EffectInstance>> _instancesByScope = new();

        public void Register(EffectInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list))
            {
                list = new List<EffectInstance>();
                _instancesByScope.Add(instance.Scope, list);
            }

            list.Add(instance);
        }

        public bool Unregister(EffectInstance instance)
        {
            if (instance == null) return false;

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list)) return false;

            return list.Remove(instance);
        }

        public LauncherEffectSnapshot BuildLauncherSnapshot(in EffectQueryContext ctx)
        {
            var snapshot = LauncherEffectSnapshot.Default;

            ApplyScopedEffects(ref snapshot, in ctx);

            return snapshot;
        }

        public ProjectileEffectSnapshot BuildProjectileSnapshot(in EffectQueryContext ctx)
        {
            var snapshot = ProjectileEffectSnapshot.Default;

            ApplyScopedEffects(ref snapshot, in ctx);

            return snapshot;
        }

        private void ApplyScopedEffects(ref LauncherEffectSnapshot snapshot, in EffectQueryContext ctx)
        {
            ApplyLauncherScope(ref snapshot, EffectScopeKey.Global());
            if (ctx.ActorId != 0) ApplyLauncherScope(ref snapshot, EffectScopeKey.Unit(ctx.ActorId));
            if (ctx.SkillId != 0) ApplyLauncherScope(ref snapshot, EffectScopeKey.SkillId(ctx.SkillId));
            if (ctx.LauncherId != 0) ApplyLauncherScope(ref snapshot, EffectScopeKey.LauncherId(ctx.LauncherId));
        }

        private void ApplyScopedEffects(ref ProjectileEffectSnapshot snapshot, in EffectQueryContext ctx)
        {
            ApplyProjectileScope(ref snapshot, EffectScopeKey.Global());
            if (ctx.ActorId != 0) ApplyProjectileScope(ref snapshot, EffectScopeKey.Unit(ctx.ActorId));
            if (ctx.SkillId != 0) ApplyProjectileScope(ref snapshot, EffectScopeKey.SkillId(ctx.SkillId));
            if (ctx.LauncherId != 0) ApplyProjectileScope(ref snapshot, EffectScopeKey.LauncherId(ctx.LauncherId));
            if (ctx.ProjectileId != 0) ApplyProjectileScope(ref snapshot, EffectScopeKey.ProjectileId(ctx.ProjectileId));
        }

        private void ApplyLauncherScope(ref LauncherEffectSnapshot snapshot, EffectScopeKey scope)
        {
            if (!_instancesByScope.TryGetValue(scope, out var list)) return;

            for (var i = 0; i < list.Count; i++)
            {
                var inst = list[i];
                ApplyLauncherInstance(ref snapshot, inst);
            }
        }

        private void ApplyProjectileScope(ref ProjectileEffectSnapshot snapshot, EffectScopeKey scope)
        {
            if (!_instancesByScope.TryGetValue(scope, out var list)) return;

            for (var i = 0; i < list.Count; i++)
            {
                var inst = list[i];
                ApplyProjectileInstance(ref snapshot, inst);
            }
        }

        private static void ApplyLauncherInstance(ref LauncherEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (var i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                switch (s.Key)
                {
                    case EffectStatKey.LauncherIntervalBaseAddFrames:
                        if (s.Op == EffectOp.Add) snapshot.IntervalBaseAddFrames += s.Value.I;
                        break;

                    case EffectStatKey.LauncherIntervalPostMul:
                        if (s.Op == EffectOp.Mul) snapshot.IntervalPostMul *= s.Value.F;
                        break;

                    case EffectStatKey.LauncherExtraProjectilesPerShot:
                        if (s.Op == EffectOp.Add) snapshot.ExtraProjectilesPerShot += s.Value.I;
                        break;

                    case EffectStatKey.LauncherExtraTotalCount:
                        if (s.Op == EffectOp.Add) snapshot.ExtraTotalCount += s.Value.I;
                        break;
                }
            }
        }

        private static void ApplyProjectileInstance(ref ProjectileEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (var i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                switch (s.Key)
                {
                    case EffectStatKey.ProjectileDamageMul:
                        if (s.Op == EffectOp.Mul) snapshot.DamageMul *= s.Value.F;
                        break;

                    case EffectStatKey.ProjectileSpeedMul:
                        if (s.Op == EffectOp.Mul) snapshot.SpeedMul *= s.Value.F;
                        break;

                    case EffectStatKey.ProjectilePierce:
                        if (s.Op == EffectOp.Add) snapshot.Pierce += s.Value.I;
                        break;
                }
            }
        }
    }
}

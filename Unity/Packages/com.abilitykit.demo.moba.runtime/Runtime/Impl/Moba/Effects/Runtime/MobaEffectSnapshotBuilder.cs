using AbilityKit.Ability.Impl.Moba.Effects.Model;
using AbilityKit.Ability.Impl.Moba.Effects.Runtime.Snapshots;
using AbilityKit.Effects.Core;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime
{
    internal static class MobaEffectSnapshotBuilder
    {
        /// <summary>
        /// 预分配的缓冲区用于减少 GC
        /// </summary>
        private static readonly EffectScopeKey[] ScopeBuffer = new EffectScopeKey[8];

        /// <summary>
        /// 构建发射器快照（合并所有相关作用域的效果）
        /// </summary>
        public static MobaLauncherEffectSnapshot BuildLauncherSnapshot(EffectRegistry registry, in MobaEffectQueryContext ctx)
        {
            var snapshot = MobaLauncherEffectSnapshot.Default;
            if (registry == null) return snapshot;

            int scopeCount = ctx.GetAllScopes(ScopeBuffer, 0);

            for (int i = 0; i < scopeCount; i++)
            {
                ApplyLauncherScope(registry, ref snapshot, ScopeBuffer[i]);
            }

            return snapshot;
        }

        /// <summary>
        /// 构建弹丸快照（合并所有相关作用域的效果）
        /// </summary>
        public static MobaProjectileEffectSnapshot BuildProjectileSnapshot(EffectRegistry registry, in MobaEffectQueryContext ctx)
        {
            var snapshot = MobaProjectileEffectSnapshot.Default;
            if (registry == null) return snapshot;

            int scopeCount = ctx.GetAllScopes(ScopeBuffer, 0);

            for (int i = 0; i < scopeCount; i++)
            {
                ApplyProjectileScope(registry, ref snapshot, ScopeBuffer[i]);
            }

            return snapshot;
        }

        private static void ApplyLauncherScope(EffectRegistry registry, ref MobaLauncherEffectSnapshot snapshot, in EffectScopeKey scope)
        {
            if (!registry.TryGetScopeList(scope, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                ApplyLauncherInstance(ref snapshot, list[i]);
            }
        }

        private static void ApplyProjectileScope(EffectRegistry registry, ref MobaProjectileEffectSnapshot snapshot, in EffectScopeKey scope)
        {
            if (!registry.TryGetScopeList(scope, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                ApplyProjectileInstance(ref snapshot, list[i]);
            }
        }

        private static void ApplyLauncherInstance(ref MobaLauncherEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (int i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                var mobaKey = (MobaEffectStatKey)s.KeyId;

                if (s.Op == EffectOp.Add)
                {
                    switch (mobaKey)
                    {
                        case MobaEffectStatKey.LauncherIntervalBaseAddFrames:
                            snapshot.IntervalBaseAddFrames += s.GetIntValue();
                            break;

                        case MobaEffectStatKey.LauncherExtraProjectilesPerShot:
                            snapshot.ExtraProjectilesPerShot += s.GetIntValue();
                            break;

                        case MobaEffectStatKey.LauncherExtraTotalCount:
                            snapshot.ExtraTotalCount += s.GetIntValue();
                            break;
                    }
                }
                else if (s.Op == EffectOp.Mul)
                {
                    if (mobaKey == MobaEffectStatKey.LauncherIntervalPostMul)
                    {
                        snapshot.IntervalPostMul *= s.GetFloatValue();
                    }
                }
            }
        }

        private static void ApplyProjectileInstance(ref MobaProjectileEffectSnapshot snapshot, EffectInstance inst)
        {
            var stats = inst?.Def?.Stats;
            if (stats == null) return;

            for (int i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null) continue;

                var mobaKey = (MobaEffectStatKey)s.KeyId;

                if (s.Op == EffectOp.Mul)
                {
                    switch (mobaKey)
                    {
                        case MobaEffectStatKey.ProjectileDamageMul:
                            snapshot.DamageMul *= s.GetFloatValue();
                            break;

                        case MobaEffectStatKey.ProjectileSpeedMul:
                            snapshot.SpeedMul *= s.GetFloatValue();
                            break;
                    }
                }
                else if (s.Op == EffectOp.Add)
                {
                    if (mobaKey == MobaEffectStatKey.ProjectilePierce)
                    {
                        snapshot.Pierce += s.GetIntValue();
                    }
                }
            }
        }
    }
}

using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Projectile;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    /// <summary>
    /// Projectile 命中诊断事件草稿生成器。
    /// 从 <see cref="MobaProjectileHitSyncHandler"/> 抽离为独立静态类，
    /// 避免诊断测试直接依赖 WorldSystemBase 所在的程序集。
    /// </summary>
    internal static class MobaProjectileHitDiagnosticProducer
    {
        public static MobaBattleDiagnosticEventDraft CreateProjectileHitDraft(
            in ProjectileHitEvent hit,
            int hitActorId,
            in ProjectileSourceContext sourceContext)
        {
            sourceContext.TryGetOrigin(out var resolvedOrigin);
            var handle = sourceContext.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : sourceContext.RootContextId;
            var contextId = sourceContext.SourceContextId != 0L
                ? sourceContext.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = $"projectileId={sourceContext.ProjectileConfigId}, hitActorId={hitActorId}, frame={hit.Frame}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.ProjectileHit,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceContext.SourceActorId,
                hitActorId,
                sourceContext.ProjectileConfigId,
                rootContextId,
                contextId,
                runtime,
                summary: summary,
                subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Projectile,
                    hit.Projectile.Value));
        }
    }
}

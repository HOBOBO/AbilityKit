using System;
using AbilityKit.Continuous;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.GameplayTags;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Buffs.Lifecycle
{
    /// <summary>
    /// Buff 运行时绑定协调器：集中维护 Buff 与 skill runtime、continuous runtime、生命周期 hook 的绑定关系。
    /// </summary>
    internal sealed class BuffRuntimeBindingCoordinator
    {
        private readonly MobaRuntimeLifecycleHookService _lifecycleHooks;
        private readonly BuffContinuousBindingService _continuousBindings;
        private readonly MobaSkillCastRuntimeService _skillRuntimes;

        public BuffRuntimeBindingCoordinator(
            MobaRuntimeLifecycleHookService lifecycleHooks,
            BuffContinuousBindingService continuousBindings,
            MobaSkillCastRuntimeService skillRuntimes)
        {
            _lifecycleHooks = lifecycleHooks;
            _continuousBindings = continuousBindings;
            _skillRuntimes = skillRuntimes;
        }

        public bool BindSkillRuntime(BuffRuntime runtime, in BuffApplyRequest request)
        {
            if (runtime == null) return false;
            if (!request.SkillRuntimeHandle.IsValid) return true;
            if (runtime.SkillRuntimeRetainHandle.IsValid) return true;

            var childId = runtime.SourceContextId;
            if (childId == 0L) return false;

            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, childId, runtime.SourceContextId, runtime.BuffId);
            var runtimeHandle = request.SkillRuntimeHandle;
            if (_skillRuntimes != null && _skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle))
            {
                new BuffRuntimeView(runtime).BindSkillRuntime(in runtimeHandle, in retainHandle);
                return true;
            }

            return false;
        }

        public void ReleaseSkillRuntime(BuffRuntime runtime)
        {
            if (runtime == null) return;
            var retainHandle = runtime.SkillRuntimeRetainHandle;
            if (!retainHandle.IsValid) return;

            try
            {
                _skillRuntimes?.ReleaseChild(in retainHandle);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffRuntimeBindingCoordinator] Release skill runtime retain failed (buffId={runtime.BuffId}, sourceContextId={runtime.SourceContextId})");
            }

            new BuffRuntimeView(runtime).ClearSkillRuntimeBinding();
        }

        public bool EnsureContinuousRuntime(BuffRuntime runtime, BuffMO buff, int sourceActorId, int targetActorId, float remainingSeconds, ContinuousTagRequirements requirements)
        {
            return _continuousBindings != null && _continuousBindings.EnsureActive(runtime, buff, sourceActorId, targetActorId, remainingSeconds, requirements);
        }

        public void EndContinuous(BuffRuntime runtime, TraceLifecycleReason reason)
        {
            if (runtime == null) return;
            var continuousReason = BuffContinuousBindingService.ToContinuousEndReason(reason);
            _continuousBindings?.End(runtime, continuousReason);
        }

        public void CleanupContinuous(global::ActorEntity target, int targetActorId, BuffRuntime runtime, bool applyRemovalTags)
        {
            if (runtime == null) return;
            _continuousBindings?.Cleanup(target, targetActorId, runtime, applyRemovalTags);
        }

        public void NotifyLifecycle(BuffRuntime runtime, MobaRuntimeLifecycleEventKind kind, string reason)
        {
            if (runtime == null || _lifecycleHooks == null) return;
            var source = runtime.ContextSource.IsValid
                ? runtime.ContextSource
                : MobaPersistentContextSourceSnapshotFactory.TryCapture(runtime, out var snapshot) && snapshot.TryGetContextSource(out var snapshotSource)
                    ? snapshotSource
                    : default;
            var lifecycleEvent = new MobaRuntimeLifecycleEvent(kind, runtime, in source, reason);
            _lifecycleHooks.Notify(in lifecycleEvent);
        }
    }
}

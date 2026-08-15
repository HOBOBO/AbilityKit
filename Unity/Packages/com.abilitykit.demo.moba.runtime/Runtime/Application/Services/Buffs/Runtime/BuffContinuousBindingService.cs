using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.GameplayTags;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Runtime {
    /// <summary>
    /// Buff 与持续行为系统的桥接层：负责创建/刷新 BuffContinuousRuntime，并在结束时解除运行时绑定。
    /// </summary>
    internal sealed class BuffContinuousBindingService
    {
        private readonly IContinuousManager _continuous;
        private readonly IMobaEffectiveTagQueryService _tags;

        public BuffContinuousBindingService(IContinuousManager continuous, IMobaEffectiveTagQueryService tags)
        {
            _continuous = continuous;
            _tags = tags;
        }

        /// <summary>
        /// 确保 Buff 拥有激活中的持续行为；已激活时只刷新投影，避免重复注册 continuous。
        /// </summary>
        public bool EnsureActive(BuffRuntime runtime, BuffMO buff, int sourceActorId, int targetActorId, float remainingSeconds, ContinuousTagRequirements requirements)
        {
            if (runtime == null) return false;
            if (buff == null) return false;

            if (runtime.Continuous == null || runtime.Continuous.IsTerminated)
            {
                runtime.Continuous = new BuffContinuousRuntime(buff, sourceActorId, targetActorId, remainingSeconds, requirements);
            }

            var wasActive = runtime.Continuous.IsActive;
            runtime.Continuous.BindRuntime(runtime);
            runtime.Continuous.BindSourceContext(runtime.SourceContextId);
            runtime.Continuous.Refresh(sourceActorId, remainingSeconds, runtime.StackCount, buff.MaxStacks, requirements);
            runtime.Continuous.IntervalRemainingSeconds = runtime.IntervalRemainingSeconds;

            if (_continuous == null) return true;
            if (wasActive)
            {
                if (_continuous is MobaContinuousManager mobaContinuous)
                {
                    mobaContinuous.Reproject(runtime.Continuous);
                }

                return true;
            }

            return _continuous.TryActivate(runtime.Continuous);
        }

        public void End(BuffRuntime runtime, ContinuousEndReason reason)
        {
            var continuous = runtime?.Continuous;
            if (continuous == null) return;

            _continuous?.TryEnd(continuous, reason);
        }

        /// <summary>
        /// 清理 Buff 与持续行为的双向引用，并按需通知标签系统重新计算移除标签。
        /// </summary>
        public void Cleanup(global::ActorEntity target, int targetActorId, BuffRuntime runtime, bool applyRemovalTags)
        {
            if (runtime == null) return;

            var continuous = runtime.Continuous;
            if (continuous != null)
            {
                if (!continuous.IsTerminated)
                {
                    End(runtime, ContinuousEndReason.CleanedUp);
                }

                if (ReferenceEquals(continuous.Runtime, runtime))
                {
                    continuous.BindRuntime(null);
                }
            }

            runtime.Continuous = null;
            runtime.TagRequirements = null;

            if (applyRemovalTags && targetActorId > 0)
            {
                _tags?.MarkDirty(targetActorId);
            }
        }

        public static ContinuousEndReason ToContinuousEndReason(TraceLifecycleReason reason)
        {
            switch (reason)
            {
                case TraceLifecycleReason.Expired:
                case TraceLifecycleReason.Completed:
                    return ContinuousEndReason.Completed;
                case TraceLifecycleReason.Dispelled:
                case TraceLifecycleReason.Interrupted:
                case TraceLifecycleReason.Cancelled:
                case TraceLifecycleReason.Dead:
                case TraceLifecycleReason.Replaced:
                case TraceLifecycleReason.Overridden:
                case TraceLifecycleReason.Failed:
                    return ContinuousEndReason.Interrupted;
                case TraceLifecycleReason.None:
                default:
                    return ContinuousEndReason.CleanedUp;
            }
        }
    }
}


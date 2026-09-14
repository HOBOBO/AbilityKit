using System;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillWindowPhase : AbilityPipelinePhaseBase<SkillPipelineContext>,
        IInterruptiblePhase<SkillPipelineContext>, IAbilityPipelinePhaseInstanceFactory<SkillPipelineContext>
    {
        private readonly SkillWindowPhaseDTO _specification;
        private readonly MobaTriggerPlanExecutor _executor;
        private double _elapsedMs;
        private int _channelTick;
        private int _recastSequence;
        private bool _closed;

        public SkillWindowPhase(
            AbilityPipelinePhaseId phaseId,
            SkillWindowPhaseDTO specification,
            MobaTriggerPlanExecutor executor)
            : base(phaseId)
        {
            _specification = specification ?? throw new ArgumentNullException(nameof(specification));
            _executor = executor;
        }

        protected override void OnExecute(SkillPipelineContext context)
        {
            _elapsedMs = 0d;
            _channelTick = 0;
            _closed = false;

            if (!TryResolve(context, out var windows, out var handle))
            {
                Fail(context, "Skill window runtime service is unavailable.");
                return;
            }

            windows.Open(in handle, _specification.WindowId, (SkillWindowKind)_specification.Kind);
            if (windows.TryGetSnapshot(in handle, out var snapshot)) _recastSequence = snapshot.RecastSequence;
            if (!ExecuteTriggers(context, _specification.OpenTriggerIds))
            {
                Close(context, MobaSkillWindowEndReason.Failed);
                return;
            }

            if ((SkillWindowKind)_specification.Kind == SkillWindowKind.Timed && _specification.DurationMs == 0)
            {
                CompleteForTimeout(context);
            }
        }

        public override void OnUpdate(SkillPipelineContext context, float deltaTime)
        {
            if (IsComplete || context == null || context.IsPaused) return;
            _elapsedMs += Math.Max(0d, deltaTime * 1000d);
            // Float frame deltas can leave an exact millisecond boundary a few
            // ten-thousandths below the integer (for example 0.65f -> 649.999976ms).
            var elapsedMs = (int)Math.Floor(_elapsedMs + 0.001d);
            var tier = ResolveChargeTier(elapsedMs, _specification.ChargeTierThresholdMs);

            if (!TryResolve(context, out var windows, out var handle))
            {
                Fail(context, "Skill window runtime service is unavailable.");
                return;
            }

            var kind = (SkillWindowKind)_specification.Kind;
            if (kind == SkillWindowKind.Channel && _specification.ChannelIntervalMs > 0)
            {
                var dueTick = elapsedMs / _specification.ChannelIntervalMs;
                while (_channelTick < dueTick && !context.IsAborted)
                {
                    _channelTick++;
                    windows.Advance(in handle, elapsedMs, tier, _channelTick);
                    if (!ExecuteTriggers(context, _specification.TickTriggerIds))
                    {
                        Close(context, MobaSkillWindowEndReason.Failed);
                        return;
                    }
                }
            }

            windows.Advance(in handle, elapsedMs, tier, _channelTick);

            if (kind == SkillWindowKind.Recast &&
                windows.TryGetSnapshot(in handle, out var snapshot) &&
                snapshot.RecastSequence > _recastSequence)
            {
                Close(context, MobaSkillWindowEndReason.Recast);
                return;
            }

            if (kind == SkillWindowKind.Charge && context.IsInputReleased())
            {
                Close(context, MobaSkillWindowEndReason.InputReleased);
                return;
            }

            if (_specification.DurationMs > 0 && elapsedMs >= _specification.DurationMs)
            {
                CompleteForTimeout(context);
            }
        }

        public void OnInterrupt(SkillPipelineContext context)
        {
            if (IsComplete) return;
            _closed = true;
            if (TryResolve(context, out var windows, out var handle))
            {
                windows.Close(in handle, MobaSkillWindowEndReason.Cancelled);
            }
            IsComplete = true;
        }

        public IAbilityPipelinePhase<SkillPipelineContext> CreateRunPhase()
        {
            return new SkillWindowPhase(PhaseId, _specification, _executor);
        }

        public override void Reset()
        {
            base.Reset();
            _elapsedMs = 0d;
            _channelTick = 0;
            _recastSequence = 0;
            _closed = false;
        }

        private void CompleteForTimeout(SkillPipelineContext context)
        {
            if (_specification.CompleteOnTimeout)
            {
                Close(context, MobaSkillWindowEndReason.Timeout);
                return;
            }

            Fail(context, string.IsNullOrEmpty(_specification.FailReason) ? "Skill window timed out." : _specification.FailReason);
            Close(context, MobaSkillWindowEndReason.Timeout, abort: true);
        }

        private void Close(SkillPipelineContext context, MobaSkillWindowEndReason reason, bool abort = false)
        {
            if (_closed) return;
            _closed = true;
            if (TryResolve(context, out var windows, out var handle)) windows.Close(in handle, reason);
            if (!ExecuteTriggers(context, _specification.CloseTriggerIds) && _specification.AbortOnTriggerFailure) abort = true;
            if (abort) context.IsAborted = true;
            Complete(context);
        }

        private bool ExecuteTriggers(SkillPipelineContext context, int[] triggerIds)
        {
            if (triggerIds == null || triggerIds.Length == 0) return true;
            for (var i = 0; i < triggerIds.Length; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;
                if (_executor != null && _executor.ExecuteRulePlan(triggerId, context)) continue;
                if (!_specification.AbortOnTriggerFailure) continue;
                Fail(context, string.IsNullOrEmpty(_specification.FailReason)
                    ? $"Skill window trigger failed: {triggerId}"
                    : _specification.FailReason);
                return false;
            }
            return true;
        }

        private static int ResolveChargeTier(int elapsedMs, int[] thresholds)
        {
            var tier = 0;
            if (thresholds == null) return tier;
            for (var i = 0; i < thresholds.Length; i++)
            {
                if (elapsedMs < thresholds[i]) break;
                tier++;
            }
            return tier;
        }

        private static bool TryResolve(
            SkillPipelineContext context,
            out MobaSkillWindowRuntimeService windows,
            out MobaSkillCastRuntimeHandle handle)
        {
            windows = null;
            handle = default;
            return context?.WorldServices != null && context.TryGetSkillRuntimeHandle(out handle) &&
                   context.WorldServices.TryResolve(out windows) && windows != null;
        }

        private void Fail(SkillPipelineContext context, string reason)
        {
            if (context == null) return;
            context.FailReason = reason;
            context.IsAborted = true;
        }
    }
}

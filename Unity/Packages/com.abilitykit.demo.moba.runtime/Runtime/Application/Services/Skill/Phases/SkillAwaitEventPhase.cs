using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillAwaitEventPhase : AbilityPipelinePhaseBase<SkillPipelineContext>,
        IInterruptiblePhase<SkillPipelineContext>, IAbilityPipelinePhaseInstanceFactory<SkillPipelineContext>
    {
        private readonly SkillAwaitEventPhaseDTO _specification;
        private double _elapsedMs;
        private bool _received;
        private IDisposable _subscription;

        public SkillAwaitEventPhase(AbilityPipelinePhaseId phaseId, SkillAwaitEventPhaseDTO specification)
            : base(phaseId)
        {
            _specification = specification ?? throw new ArgumentNullException(nameof(specification));
        }

        protected override void OnExecute(SkillPipelineContext context)
        {
            _elapsedMs = 0d;
            _received = false;
            DisposeSubscription();
            if (context?.EventBus == null)
            {
                Fail(context, "Skill event bus is unavailable.");
                return;
            }

            var key = new EventKey<object>(TriggeringIdUtil.GetEventEid(_specification.EventId));
            _subscription = context.EventBus.Subscribe(key, payload => OnEvent(context, payload));
        }

        public override void OnUpdate(SkillPipelineContext context, float deltaTime)
        {
            if (IsComplete || context == null || context.IsPaused) return;
            if (_received)
            {
                Complete(context);
                return;
            }

            if (_specification.TimeoutMs <= 0) return;
            _elapsedMs += Math.Max(0d, deltaTime * 1000d);
            if (_elapsedMs >= _specification.TimeoutMs) HandleTimeout(context);
        }

        public void OnInterrupt(SkillPipelineContext context)
        {
            if (IsComplete) return;
            DisposeSubscription();
            IsComplete = true;
        }

        public IAbilityPipelinePhase<SkillPipelineContext> CreateRunPhase()
        {
            return new SkillAwaitEventPhase(PhaseId, _specification);
        }

        public override void Reset()
        {
            base.Reset();
            _elapsedMs = 0d;
            _received = false;
            DisposeSubscription();
        }

        protected override void OnExit(SkillPipelineContext context)
        {
            DisposeSubscription();
        }

        private void OnEvent(SkillPipelineContext context, object payload)
        {
            if (IsComplete || _received || !MatchesFilters(context, payload)) return;
            if (!TryCapturePayload(context, payload, out var failure))
            {
                Fail(context, failure);
                return;
            }
            _received = true;
            if (context?.WorldServices != null && context.TryGetSkillRuntimeHandle(out var handle) &&
                context.WorldServices.TryResolve<MobaSkillWindowRuntimeService>(out var windows) && windows != null)
            {
                windows.SignalEvent(in handle, _specification.EventId);
            }
        }

        private bool TryCapturePayload(SkillPipelineContext context, object payload, out string failure)
        {
            failure = null;
            var captures = _specification.Captures;
            if (captures == null || captures.Length == 0) return true;
            if (context?.WorldServices == null ||
                !context.WorldServices.TryResolve<IPayloadAccessorRegistry>(out var accessors) || accessors == null ||
                !context.TryGetSkillRuntimeHandle(out var handle) ||
                !context.WorldServices.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null ||
                !runtimes.TryGetBlackboard(in handle, out var blackboard) || blackboard == null)
            {
                failure = "Skill event capture requires payload accessors and a live skill runtime Blackboard.";
                return false;
            }

            for (var i = 0; i < captures.Length; i++)
            {
                var capture = captures[i];
                if (capture == null) continue;
                if (!TryCaptureValue(accessors, payload, capture, blackboard, context, out var error))
                {
                    if (!capture.Required) continue;
                    failure = $"Skill event capture failed at index {i}: {error}";
                    return false;
                }
            }
            return true;
        }

        private static bool TryCaptureValue(
            IPayloadAccessorRegistry accessors,
            object payload,
            SkillEventCaptureDTO capture,
            MobaSkillRuntimeBlackboard blackboard,
            SkillPipelineContext context,
            out string error)
        {
            error = null;
            if (capture.FieldId <= 0 || string.IsNullOrWhiteSpace(capture.Key))
            {
                error = "field id and key are required.";
                return false;
            }

            var scope = (SkillEventCaptureScope)capture.Scope;
            var runtimeScope = scope == SkillEventCaptureScope.Target
                ? MobaSkillRuntimeBlackboardScope.Target
                : MobaSkillRuntimeBlackboardScope.Cast;
            var ownerId = runtimeScope == MobaSkillRuntimeBlackboardScope.Target ? context.TargetActorId : 0L;
            if (runtimeScope == MobaSkillRuntimeBlackboardScope.Target && ownerId <= 0)
            {
                error = "target-scoped capture requires a target actor.";
                return false;
            }

            var scopeName = scope == SkillEventCaptureScope.Target ? "target" : "cast";
            var keyName = capture.Key.Trim();
            var keyId = BlackboardIdMapper.KeyId($"skill_runtime.{scopeName}.{keyName}");
            var address = new MobaSkillRuntimeBlackboardAddress(runtimeScope, ownerId);
            if ((SkillEventCaptureValueType)capture.ValueType == SkillEventCaptureValueType.Integer)
            {
                if (!accessors.TryGetInt(in payload, capture.FieldId, out var value))
                {
                    error = $"payload field {capture.FieldId} is not an integer.";
                    return false;
                }
                var key = new MobaSkillRuntimeBlackboardKey(keyId, keyName, MobaSkillRuntimeValueKind.Int,
                    runtimeScope, MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
                var runtimeValue = MobaSkillRuntimeValue.FromInt(value);
                return blackboard.Set(in key, in runtimeValue, in address);
            }

            if (!accessors.TryGetDouble(in payload, capture.FieldId, out var number))
            {
                error = $"payload field {capture.FieldId} is not numeric.";
                return false;
            }
            var numberKey = new MobaSkillRuntimeBlackboardKey(keyId, keyName, MobaSkillRuntimeValueKind.Double,
                runtimeScope, MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            var numberValue = MobaSkillRuntimeValue.FromDouble(number);
            return blackboard.Set(in numberKey, in numberValue, in address);
        }

        private bool MatchesFilters(SkillPipelineContext context, object payload)
        {
            var filters = _specification.Filters;
            if (filters == null || filters.Length == 0) return true;
            if (context?.WorldServices == null ||
                !context.WorldServices.TryResolve<IPayloadAccessorRegistry>(out var accessors) || accessors == null)
            {
                return false;
            }

            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.FieldId <= 0) return false;
                var expected = filter.UseCasterActorId ? context.CasterActorId :
                    filter.UseTargetActorId ? context.TargetActorId :
                    filter.UseSkillId ? context.SkillId : filter.ExpectedValue;
                if (!accessors.TryGetInt(in payload, filter.FieldId, out var actual) || actual != expected) return false;
            }
            return true;
        }

        private void HandleTimeout(SkillPipelineContext context)
        {
            if (_specification.CompleteOnTimeout)
            {
                Complete(context);
                return;
            }

            Fail(context, $"Skill event wait timed out: {_specification.EventId}");
        }

        private void Fail(SkillPipelineContext context, string reason)
        {
            DisposeSubscription();
            if (context != null)
            {
                context.FailReason = reason;
                context.IsAborted = true;
            }
            IsComplete = true;
        }

        private void DisposeSubscription()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}

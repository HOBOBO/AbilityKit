using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
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
            _received = true;
            if (context?.WorldServices != null && context.TryGetSkillRuntimeHandle(out var handle) &&
                context.WorldServices.TryResolve<MobaSkillWindowRuntimeService>(out var windows) && windows != null)
            {
                windows.SignalEvent(in handle, _specification.EventId);
            }
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

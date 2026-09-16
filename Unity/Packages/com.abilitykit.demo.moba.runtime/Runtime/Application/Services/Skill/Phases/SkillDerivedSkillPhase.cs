using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillDerivedSkillPhase : AbilityPipelinePhaseBase<SkillPipelineContext>,
        IInterruptiblePhase<SkillPipelineContext>, IAbilityPipelinePhaseInstanceFactory<SkillPipelineContext>
    {
        private readonly SkillDerivedSkillPhaseDTO _specification;
        private MobaSkillCastRuntimeHandle _child;

        public SkillDerivedSkillPhase(AbilityPipelinePhaseId phaseId, SkillDerivedSkillPhaseDTO specification)
            : base(phaseId)
        {
            _specification = specification ?? throw new ArgumentNullException(nameof(specification));
        }

        protected override void OnExecute(SkillPipelineContext context)
        {
            _child = default;
            if (context?.WorldServices == null ||
                !context.TryGetSkillRuntimeHandle(out var parent) ||
                !context.WorldServices.TryResolve<MobaDerivedSkillService>(out var derived) || derived == null ||
                !context.WorldServices.TryResolve<SkillCastCoordinator>(out var coordinator) || coordinator == null)
            {
                FinishFailure(context, "Derived skill services or parent runtime are unavailable.");
                return;
            }
            if (_specification.SkillId <= 0)
            {
                FinishFailure(context, "Derived skill id must be positive.");
                return;
            }
            if (!derived.CanStart(in parent, _specification.MaxDepth, out var guardFailure))
            {
                FinishFailure(context, guardFailure);
                return;
            }

            var aimPos = _specification.InheritAim ? context.AimPos : Vec3.Zero;
            var aimDir = _specification.InheritAim ? context.AimDir : Vec3.Zero;
            var targetActorId = _specification.InheritTarget ? context.TargetActorId : 0;
            var result = coordinator.TryCastDerivedSkill(
                context.CasterActorId, _specification.SkillId, in aimPos, in aimDir, targetActorId);
            if (!result.Success)
            {
                FinishFailure(context, result.FailReason ?? "Derived skill cast was rejected.");
                return;
            }

            _child = result.RuntimeHandle;
            if (_child.IsValid && !derived.Link(in parent, in _child, _specification.MaxDepth, out var linkFailure))
            {
                derived.TerminateUnlinkedChild(in _child);
                _child = default;
                FinishFailure(context, linkFailure);
                return;
            }
            if (!_specification.WaitForCompletion || !_child.IsValid) Complete(context);
        }

        public override void OnUpdate(SkillPipelineContext context, float deltaTime)
        {
            if (IsComplete || !_specification.WaitForCompletion || !_child.IsValid) return;
            if (context?.WorldServices == null ||
                !context.WorldServices.TryResolve<MobaDerivedSkillService>(out var derived) || derived == null ||
                !derived.IsRunning(in _child))
            {
                Complete(context);
            }
        }

        public void OnInterrupt(SkillPipelineContext context)
        {
            IsComplete = true;
        }

        public IAbilityPipelinePhase<SkillPipelineContext> CreateRunPhase()
        {
            return new SkillDerivedSkillPhase(PhaseId, _specification);
        }

        public override void Reset()
        {
            base.Reset();
            _child = default;
        }

        private void FinishFailure(SkillPipelineContext context, string reason)
        {
            if (_specification.AbortOnFailure && context != null)
            {
                context.FailReason = string.IsNullOrWhiteSpace(_specification.FailReason)
                    ? reason
                    : _specification.FailReason;
                context.IsAborted = true;
            }
            IsComplete = true;
        }
    }
}

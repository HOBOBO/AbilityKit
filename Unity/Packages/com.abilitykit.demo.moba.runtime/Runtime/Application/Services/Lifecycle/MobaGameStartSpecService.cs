using System;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Protocol.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IMobaPendingGameStartSpecStore))]
    [WorldService(typeof(MobaGameStartSpecService))]
    public sealed class MobaGameStartSpecService : IService, IMobaPendingGameStartSpecStore
    {
        private MobaGameStartSpec _spec;
        private MobaBattleStartPlan _plan;

        public bool HasSpec { get; private set; }
        public bool HasPlan { get; private set; }

        public void Set(in MobaGameStartSpec spec)
        {
            var validation = ValidateSpec(in spec);
            if (!validation.Succeeded)
            {
                throw new System.InvalidOperationException("invalid battle game start spec. " + validation.Message);
            }

            var plan = MobaBattleStartPlan.FromEnterReq(in spec.EnterReq);
            var planValidation = ValidatePlan(in plan);
            if (!planValidation.Succeeded)
            {
                throw new System.InvalidOperationException("invalid battle start plan. " + planValidation.Message);
            }

            _spec = spec;
            _plan = plan;
            HasSpec = true;
            HasPlan = true;
        }

        public bool TryGet(out MobaGameStartSpec spec)
        {
            spec = _spec;
            return HasSpec;
        }

        public bool TryGetPlan(out MobaBattleStartPlan plan)
        {
            plan = _plan;
            return HasPlan;
        }

        public MobaBattleStartPlanValidationResult ValidatePendingPlan()
        {
            if (!HasPlan)
            {
                return MobaBattleStartPlanValidationResult.Fail("pending battle start plan is missing.");
            }

            return ValidatePlan(in _plan);
        }

        public MobaGameStartSpecValidationResult ValidatePendingSpec()
        {
            if (!HasSpec)
            {
                return MobaGameStartSpecValidationResult.Fail("pending battle game start spec is missing.");
            }

            return ValidateSpec(in _spec);
        }

        public static MobaGameStartSpecValidationResult ValidateSpec(in MobaGameStartSpec spec)
        {
            var enterReq = spec.EnterReq;
            var enterValidation = MobaProtocolValidation.ValidateEnterGameReq(in enterReq);
            if (!enterValidation.IsValid)
            {
                return MobaGameStartSpecValidationResult.Fail("enter-game request invalid. " + enterValidation);
            }

            return MobaGameStartSpecValidationResult.Success;
        }

        public static MobaBattleStartPlanValidationResult ValidatePlan(
            in MobaBattleStartPlan plan)
        {
            return MobaBattleStartPlanValidator.Validate(in plan);
        }

        public void Clear()
        {
            _spec = default;
            _plan = default;
            HasSpec = false;
            HasPlan = false;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}


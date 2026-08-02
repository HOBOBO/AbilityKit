using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public readonly struct MobaGameStartSpecValidationResult
    {
        public static readonly MobaGameStartSpecValidationResult Success = new MobaGameStartSpecValidationResult(true, null);

        public readonly bool Succeeded;
        public readonly string Message;

        public MobaGameStartSpecValidationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public static MobaGameStartSpecValidationResult Fail(string message)
        {
            return new MobaGameStartSpecValidationResult(false, message);
        }
    }

    public readonly struct MobaBattleStartPlanValidationResult
    {
        public static readonly MobaBattleStartPlanValidationResult Success = new MobaBattleStartPlanValidationResult(true, null);

        public readonly bool Succeeded;
        public readonly string Message;

        public MobaBattleStartPlanValidationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public static MobaBattleStartPlanValidationResult Fail(string message)
        {
            return new MobaBattleStartPlanValidationResult(false, message);
        }
    }

    public interface IMobaPendingGameStartSpecStore
    {
        bool HasSpec { get; }

        bool HasPlan { get; }

        void Set(in MobaGameStartSpec spec);

        bool TryGet(out MobaGameStartSpec spec);

        bool TryGetPlan(out MobaBattleStartPlan plan);

        MobaBattleStartPlanValidationResult ValidatePendingPlan();

        MobaGameStartSpecValidationResult ValidatePendingSpec();

        void Clear();
    }
}

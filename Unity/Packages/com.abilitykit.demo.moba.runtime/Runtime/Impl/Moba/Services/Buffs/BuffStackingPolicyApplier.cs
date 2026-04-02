using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.Moba.Components;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffStackingPolicyApplier
    {
        public bool ApplyToExisting(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds, BuffContextService context)
        {
            if (existing == null) return false;
            if (buff == null) return false;

            switch (buff.StackingPolicy)
            {
                case BuffStackingPolicy.IgnoreIfExists:
                    return false;
                case BuffStackingPolicy.Replace:
                    context?.CancelAndEnd(existing);
                    existing.SourceId = sourceActorId;
                    existing.StackCount = 0;
                    existing.Remaining = durationSeconds;
                    AddStack(existing, buff.MaxStacks);
                    return true;
                case BuffStackingPolicy.AddStack:
                    AddStack(existing, buff.MaxStacks);
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = sourceActorId;
                    return true;
                case BuffStackingPolicy.RefreshDuration:
                    RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
                    existing.SourceId = sourceActorId;
                    return true;
                case BuffStackingPolicy.None:
                default:
                    return false;
            }
        }

        public BuffRuntime CreateNewRuntime(BuffMO buff, int sourceActorId, float durationSeconds)
        {
            var rt = new BuffRuntime
            {
                BuffId = buff.Id,
                Remaining = durationSeconds,
                IntervalRemainingSeconds = 0,
                SourceId = sourceActorId,
                StackCount = 0,
                SourceContextId = 0,
            };

            AddStack(rt, buff.MaxStacks);
            ResetInterval(rt, buff);
            return rt;
        }

        public static void ResetInterval(BuffRuntime rt, BuffMO buff)
        {
            if (rt == null) return;
            if (buff == null) return;
            rt.IntervalRemainingSeconds = buff.IntervalMs > 0 ? buff.IntervalMs / 1000f : 0f;
        }

        private static void RefreshRemaining(BuffRuntime rt, BuffRefreshPolicy policy, float durationSeconds)
        {
            if (rt == null) return;

            switch (policy)
            {
                case BuffRefreshPolicy.ResetRemaining:
                    rt.Remaining = durationSeconds;
                    return;
                case BuffRefreshPolicy.AddRemaining:
                    rt.Remaining += durationSeconds;
                    return;
                case BuffRefreshPolicy.KeepRemaining:
                case BuffRefreshPolicy.None:
                default:
                    return;
            }
        }

        private static void AddStack(BuffRuntime rt, int maxStacks)
        {
            if (rt == null) return;

            if (maxStacks <= 0) maxStacks = int.MaxValue;
            if (rt.StackCount >= maxStacks) return;

            rt.StackCount++;
        }
    }
}

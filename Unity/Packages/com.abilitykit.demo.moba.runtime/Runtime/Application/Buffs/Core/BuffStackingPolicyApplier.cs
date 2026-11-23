using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services.Buffs.Core {
    internal enum BuffStackingApplyOutcome
    {
        Ignored = 0,
        Replaced = 1,
        StackAdded = 2,
        DurationRefreshed = 3,
    }

    internal readonly struct BuffStackingApplyResult
    {
        public static readonly BuffStackingApplyResult Ignored = new BuffStackingApplyResult(false, BuffStackingApplyOutcome.Ignored, string.Empty);

        public BuffStackingApplyResult(bool applied, BuffStackingApplyOutcome outcome, string rejectCode)
        {
            Applied = applied;
            Outcome = outcome;
            RejectCode = rejectCode;
        }

        public bool Applied { get; }
        public BuffStackingApplyOutcome Outcome { get; }
        public string RejectCode { get; }
        public bool IsReplace => Outcome == BuffStackingApplyOutcome.Replaced;
        public bool ShouldResetInterval => Applied;

        public static BuffStackingApplyResult AppliedResult(BuffStackingApplyOutcome outcome)
        {
            return new BuffStackingApplyResult(true, outcome, string.Empty);
        }
    }

    internal delegate BuffStackingApplyResult BuffStackingRule(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds);

    /// <summary>
    /// Buff 叠层策略执行器：只修改堆叠数、剩余时间和来源，不处理事件、上下文和持续行为。
    /// </summary>
    internal sealed class BuffStackingPolicyApplier
    {
        private readonly Dictionary<BuffStackingPolicy, BuffStackingRule> _rules = new Dictionary<BuffStackingPolicy, BuffStackingRule>
        {
            [BuffStackingPolicy.IgnoreIfExists] = Ignore,
            [BuffStackingPolicy.Replace] = Replace,
            [BuffStackingPolicy.AddStack] = AddStackRule,
            [BuffStackingPolicy.RefreshDuration] = RefreshDuration,
            [BuffStackingPolicy.None] = Ignore,
        };

        /// <summary>
        /// 对已存在运行时应用配置中的叠层/刷新策略，返回结构化结果供生命周期层判断后续绑定和通知。
        /// </summary>
        public BuffStackingApplyResult ApplyToExisting(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds)
        {
            if (existing == null) return BuffStackingApplyResult.Ignored;
            if (buff == null) return BuffStackingApplyResult.Ignored;

            if (!_rules.TryGetValue(buff.StackingPolicy, out var rule) || rule == null)
            {
                return BuffStackingApplyResult.Ignored;
            }

            return rule(existing, buff, sourceActorId, durationSeconds);
        }

        /// <summary>
        /// 从对象池创建新运行时，并按新实例语义初始化基础状态。
        /// </summary>
        public BuffRuntime CreateNewRuntime(BuffMO buff, int sourceActorId, float durationSeconds)
        {
            var rt = BuffRepository.RentRuntime();
            rt.BuffId = buff.Id;
            rt.Remaining = durationSeconds;
            rt.IntervalRemainingSeconds = 0;
            rt.SourceId = sourceActorId;
            rt.StackCount = 0;
            rt.SourceContextId = 0;

            AddStack(rt, buff.MaxStacks);
            ResetInterval(rt, buff);
            return rt;
        }

        public static void ResetInterval(BuffRuntime rt, BuffMO buff)
        {
            if (rt == null) return;
            if (buff == null) return;

            var intervalRemainingSeconds = buff.IntervalMs > 0 ? buff.IntervalMs / 1000f : 0f;
            rt.IntervalRemainingSeconds = intervalRemainingSeconds;
            if (rt.Continuous != null)
            {
                rt.Continuous.IntervalRemainingSeconds = intervalRemainingSeconds;
            }
        }

        private static BuffStackingApplyResult Ignore(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds)
        {
            return BuffStackingApplyResult.Ignored;
        }

        private static BuffStackingApplyResult Replace(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds)
        {
            existing.SourceId = sourceActorId;
            existing.StackCount = 0;
            existing.Remaining = durationSeconds;
            AddStack(existing, buff.MaxStacks);
            return BuffStackingApplyResult.AppliedResult(BuffStackingApplyOutcome.Replaced);
        }

        private static BuffStackingApplyResult AddStackRule(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds)
        {
            AddStack(existing, buff.MaxStacks);
            RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
            existing.SourceId = sourceActorId;
            return BuffStackingApplyResult.AppliedResult(BuffStackingApplyOutcome.StackAdded);
        }

        private static BuffStackingApplyResult RefreshDuration(BuffRuntime existing, BuffMO buff, int sourceActorId, float durationSeconds)
        {
            RefreshRemaining(existing, buff.RefreshPolicy, durationSeconds);
            existing.SourceId = sourceActorId;
            return BuffStackingApplyResult.AppliedResult(BuffStackingApplyOutcome.DurationRefreshed);
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


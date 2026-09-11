#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Action;

namespace AbilityKit.Samples.Logic.Ability.Samples.Action
{
    /// <summary>
    /// Buff 动作示例，实现 IAction 接口。
    /// </summary>
    public sealed class BuffAction : IAction
    {
        private string _buffId;
        private int _durationSeconds;
        private int _stacks;

        public string ActionId => "buff";
        public string DisplayName => "施加Buff";

        private bool _cancelled;

        public BuffAction()
        {
            _buffId = string.Empty;
            _durationSeconds = 0;
            _stacks = 1;
            _cancelled = false;
        }

        public ActionResult Execute(IActionContext context)
        {
            if (_cancelled)
                return ActionResult.Failed("Action was cancelled");

            if (string.IsNullOrEmpty(_buffId))
            {
                return ActionResult.Failed("No buff specified");
            }

            var target = context.Target as ITarget;
            if (target == null)
            {
                return ActionResult.Failed("No valid target");
            }

            var duration = context.GetArg<int>("duration", _durationSeconds);
            var stacks = context.GetArg<int>("stacks", _stacks);

            Console.WriteLine($"[BuffAction] Applying buff '{_buffId}' to target");
            Console.WriteLine($"[BuffAction] Duration: {duration}s, Stacks: {stacks}");

            return ActionResult.Succeeded(new BuffResult(_buffId, duration, stacks));
        }

        public bool TryCancel()
        {
            _cancelled = true;
            return true;
        }

        /// <summary>
        /// 设置 Buff ID。
        /// </summary>
        public BuffAction SetBuffId(string buffId)
        {
            _buffId = buffId;
            return this;
        }

        /// <summary>
        /// 设置持续时间。
        /// </summary>
        public BuffAction SetDuration(int durationSeconds)
        {
            _durationSeconds = durationSeconds;
            return this;
        }

        /// <summary>
        /// 设置层数。
        /// </summary>
        public BuffAction SetStacks(int stacks)
        {
            _stacks = stacks;
            return this;
        }

        public static BuffAction FromArgs(IReadOnlyDictionary<string, object> args)
        {
            var buffId = args.TryGetValue("buff_id", out var b) ? b?.ToString() ?? "" : "";
            var duration = args.TryGetValue("duration", out var d) ? Convert.ToInt32(d) : 0;
            var stacks = args.TryGetValue("stacks", out var s) ? Convert.ToInt32(s) : 1;
            return new BuffAction().SetBuffId(buffId).SetDuration(duration).SetStacks(stacks);
        }
    }

    /// <summary>
    /// Buff 结果。
    /// </summary>
    public readonly struct BuffResult
    {
        public string BuffId { get; }
        public int DurationSeconds { get; }
        public int Stacks { get; }

        public BuffResult(string buffId, int durationSeconds, int stacks)
        {
            BuffId = buffId;
            DurationSeconds = durationSeconds;
            Stacks = stacks;
        }
    }

    /// <summary>
    /// Buff 动作工厂。
    /// </summary>
    public sealed class BuffActionFactory : IActionFactory
    {
        public string FactoryId => "buff_factory";

        public bool CanCreate(string actionType) => actionType == "buff";

        public IAction Create(string actionType, IReadOnlyDictionary<string, object> args)
            => BuffAction.FromArgs(args);
    }
}

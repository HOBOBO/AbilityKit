#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Samples.Logic.Ability.Core.Pipeline
{
    /// <summary>
    /// 管线阶段的基类，提供通用的生命周期管理。
    /// 继承此类可以简化阶段实现。
    /// </summary>
    public abstract class PipelinePhaseBase : IPipelinePhase
    {
        public abstract string PhaseId { get; }
        public virtual int Priority => 0;

        public virtual PhaseResult Execute(IPipelineContext context)
        {
            if (!CanExecute(context))
            {
                return PhaseResult.Skip;
            }

            OnEnter(context);
            var result = OnExecute(context);
            if (result == PhaseResult.Success)
            {
                OnComplete(context);
            }
            else if (result == PhaseResult.Failure)
            {
                OnFail(context);
            }

            return result;
        }

        public virtual async Task<PhaseResult> ExecuteAsync(IPipelineContext context)
        {
            if (!CanExecute(context))
            {
                return PhaseResult.Skip;
            }

            OnEnter(context);
            var result = await OnExecuteAsync(context);
            if (result == PhaseResult.Success)
            {
                OnComplete(context);
            }
            else if (result == PhaseResult.Failure)
            {
                OnFail(context);
            }

            return result;
        }

        /// <summary>
        /// 检查阶段是否可以执行。
        /// </summary>
        protected virtual bool CanExecute(IPipelineContext context) => true;

        /// <summary>
        /// 阶段进入时的回调。
        /// </summary>
        protected virtual void OnEnter(IPipelineContext context) { }

        /// <summary>
        /// 阶段执行逻辑。
        /// </summary>
        protected abstract PhaseResult OnExecute(IPipelineContext context);

        /// <summary>
        /// 异步执行阶段逻辑。
        /// </summary>
        protected virtual Task<PhaseResult> OnExecuteAsync(IPipelineContext context)
            => Task.FromResult(OnExecute(context));

        /// <summary>
        /// 阶段成功完成时的回调。
        /// </summary>
        protected virtual void OnComplete(IPipelineContext context) { }

        /// <summary>
        /// 阶段失败时的回调。
        /// </summary>
        protected virtual void OnFail(IPipelineContext context) { }
    }

    /// <summary>
    /// 时间线阶段，支持基于时间的阶段性执行。
    /// 适用于技能动画、演出效果等场景。
    /// </summary>
    public abstract class TimelinePhaseBase : PipelinePhaseBase
    {
        private int _nextEventIndex;
        private bool _started;

        protected abstract int DurationMs { get; }
        protected abstract TimelineEvent[] GetTimelineEvents();

        public override PhaseResult Execute(IPipelineContext context)
        {
            if (!CanExecute(context))
            {
                return PhaseResult.Skip;
            }

            var elapsedMs = context.GetData<int>("elapsed_ms");
            if (elapsedMs >= DurationMs)
            {
                return PhaseResult.Success;
            }

            if (!_started)
            {
                OnEnter(context);
                _started = true;
                _nextEventIndex = 0;
            }

            return OnUpdate(context, elapsedMs);
        }

        protected virtual PhaseResult OnUpdate(IPipelineContext context, int elapsedMs)
        {
            var events = GetTimelineEvents();
            while (_nextEventIndex < events.Length)
            {
                var e = events[_nextEventIndex];
                if (elapsedMs < e.AtMs) break;

                OnTimelineEvent(context, e);
                _nextEventIndex++;
            }

            if (elapsedMs >= DurationMs)
            {
                OnComplete(context);
                return PhaseResult.Success;
            }

            return PhaseResult.Pending;
        }

        protected abstract void OnTimelineEvent(IPipelineContext context, TimelineEvent e);

        protected sealed override void OnEnter(IPipelineContext context)
        {
            _started = false;
            _nextEventIndex = 0;
            base.OnEnter(context);
        }
    }

    /// <summary>
    /// 即时阶段，在一帧内完成执行。
    /// 适用于检查条件、应用效果等快速操作。
    /// </summary>
    public abstract class InstantPhaseBase : PipelinePhaseBase
    {
        protected override sealed PhaseResult OnExecute(IPipelineContext context)
        {
            return OnExecuteInstant(context) ? PhaseResult.Success : PhaseResult.Failure;
        }

        /// <summary>
        /// 执行即时逻辑。
        /// </summary>
        protected abstract bool OnExecuteInstant(IPipelineContext context);
    }
}

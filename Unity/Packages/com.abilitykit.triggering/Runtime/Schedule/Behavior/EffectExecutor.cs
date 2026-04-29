using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度效果执行器
    /// 适配 IScheduleEffect 到 IScheduleExecutor
    /// </summary>
    public sealed class EffectExecutor : IScheduleExecutor
    {
        private readonly IScheduleEffect _effect;

        public EffectExecutor(IScheduleEffect effect)
        {
            _effect = effect;
        }

        public bool TryExecute(in ScheduleItemData item, in ScheduleContext context)
        {
            if (_effect == null) return false;
            if (!_effect.CanExecute(context)) return false;

            _effect.Execute(context);
            return true;
        }
    }
}

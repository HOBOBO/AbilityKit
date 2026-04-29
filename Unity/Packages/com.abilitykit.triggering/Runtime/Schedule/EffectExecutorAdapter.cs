using System;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 适配器：将 EffectExecutor 桥接到 DefaultScheduleManager
    /// </summary>
    internal sealed class EffectExecutorAdapter : IScheduleExecutor
    {
        private readonly DefaultScheduleManager _manager;

        public EffectExecutorAdapter(DefaultScheduleManager manager)
        {
            _manager = manager;
        }

        public bool TryExecute(in ScheduleItemData item, in ScheduleContext context)
        {
            int index = item.Handle.Index;
            if (index < 0 || index >= _manager.ItemCount)
                return false;

            var effect = _manager.GetEffect(index);
            if (effect == null) return false;
            if (!effect.CanExecute(context)) return false;

            effect.Execute(context);
            return true;
        }
    }
}

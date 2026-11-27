using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 持有单个调度分组中已登记的实例及其索引，不取消实例或将其归还对象池。
    /// </summary>
    internal sealed class ActionSchedulerInstances
    {
        private readonly List<ActionInstance> _actions = new();
        private readonly Dictionary<int, ActionInstance> _instancesByPlanIndex = new();
        private readonly Dictionary<int, int> _planIndexByInstanceId = new();

        /// <summary>
        /// 返回仍在列表中的实例数，包括等待下一次推进清理的终态实例。
        /// </summary>
        internal int Count => _actions.Count;

        /// <summary>
        /// 读取指定位置的实例，供调度器按既有顺序推进或清理。
        /// </summary>
        internal ActionInstance this[int index] => _actions[index];

        /// <summary>
        /// 将实例登记到列表尾部，不占用计划槽位。
        /// </summary>
        internal void Attach(ActionInstance instance)
        {
            _actions.Add(instance);
        }

        /// <summary>
        /// 将新实例登记到列表尾部并关联槽位，再摘除旧实例并交还调度器清理。
        /// </summary>
        internal ActionInstance Replace(int planIndex, ActionInstance instance)
        {
            _instancesByPlanIndex.TryGetValue(planIndex, out var previous);
            Attach(instance);
            _instancesByPlanIndex[planIndex] = instance;
            _planIndexByInstanceId[instance.InstanceId] = planIndex;

            if (previous != null)
            {
                DetachAt(_actions.IndexOf(previous));
            }

            return previous;
        }

        /// <summary>
        /// 摘除实例和它仍占用的索引；不能清除已由新实例接管的槽位。
        /// </summary>
        internal void DetachAt(int index)
        {
            var instance = _actions[index];
            _actions.RemoveAt(index);
            if (_planIndexByInstanceId.TryGetValue(instance.InstanceId, out var planIndex))
            {
                if (_instancesByPlanIndex.TryGetValue(planIndex, out var indexed) && ReferenceEquals(indexed, instance))
                {
                    _instancesByPlanIndex.Remove(planIndex);
                }

                _planIndexByInstanceId.Remove(instance.InstanceId);
            }
        }
    }
}

using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 保存单次注册的触发器和排序键；同一触发器可以有多条独立注册。
    /// </summary>
    internal readonly struct TriggerRunnerEntry<TArgs, TCtx>
    {
        public readonly int Phase;
        public readonly int Priority;
        public readonly long Order;
        public readonly ITrigger<TArgs, TCtx> Trigger;

        public TriggerRunnerEntry(int phase, int priority, long order, ITrigger<TArgs, TCtx> trigger)
        {
            Phase = phase;
            Priority = priority;
            Order = order;
            Trigger = trigger;
        }
    }

    /// <summary>
    /// 将新注册插入有序列表，避免在事件派发时重新排序。
    /// </summary>
    internal static class TriggerRunnerEntryList
    {
        public static void InsertSorted<TArgs, TCtx>(List<TriggerRunnerEntry<TArgs, TCtx>> list, TriggerRunnerEntry<TArgs, TCtx> entry)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var other = list[i];
                if (entry.Phase < other.Phase)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority > other.Priority)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority == other.Priority && entry.Order < other.Order)
                {
                    list.Insert(i, entry);
                    return;
                }
            }

            list.Add(entry);
        }
    }
}

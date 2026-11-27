using System;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    public sealed partial class TriggerRunner<TCtx>
    {
        /// <summary>
        /// 持有单次注册的释放责任；事件槽统一处理实际注销和退订。
        /// </summary>
        private sealed class TriggerRunnerRegistration<TArgs> : IDisposable
        {
            private EventSlot<TArgs> _slot;
            private TriggerRunnerEntry<TArgs, TCtx> _entry;
            private readonly EventKey<TArgs> _key;

            public TriggerRunnerRegistration(EventSlot<TArgs> slot, EventKey<TArgs> key, TriggerRunnerEntry<TArgs, TCtx> entry)
            {
                _slot = slot;
                _key = key;
                _entry = entry;
            }

            /// <summary>
            /// 先清除句柄持有的注册，再转交事件槽，以便回调中重复释放时保持幂等。
            /// </summary>
            public void Dispose()
            {
                var slot = _slot;
                if (slot == null) return;

                var entry = _entry;
                _slot = null;
                _entry = null;
                slot.Unregister(_key, entry);
            }
        }
    }
}

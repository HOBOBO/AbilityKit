using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 持有单次注册的释放责任；移除该条目后通知生命周期，再按移除时的列表状态退订。
    /// </summary>
    internal sealed class TriggerRunnerRegistration<TArgs, TCtx> : IDisposable
    {
        private List<TriggerRunnerEntry<TArgs, TCtx>> _list;
        private TriggerRunnerEntry<TArgs, TCtx> _entry;
        private readonly EventKey<TArgs> _key;
        private readonly IDisposable _subscription;
        private readonly ITriggerLifecycle<TCtx> _lifecycle;
        private readonly Action<EventKey<TArgs>, IDisposable> _unsubscribe;
        private bool _disposed;

        public TriggerRunnerRegistration(
            List<TriggerRunnerEntry<TArgs, TCtx>> list,
            TriggerRunnerEntry<TArgs, TCtx> entry,
            EventKey<TArgs> key,
            IDisposable subscription,
            ITriggerLifecycle<TCtx> lifecycle,
            Action<EventKey<TArgs>, IDisposable> unsubscribe)
        {
            _list = list;
            _entry = entry;
            _key = key;
            _subscription = subscription;
            _lifecycle = lifecycle;
            _unsubscribe = unsubscribe;
        }

        /// <summary>
        /// 幂等移除条目；注销通知异常保持向上抛出，不保证随后完成退订。
        /// </summary>
        public void Dispose()
        {
            if (_list == null || _disposed) return;
            _disposed = true;

            int removeIndex = -1;
            for (int i = 0; i < _list.Count; i++)
            {
                if (!ReferenceEquals(_list[i].Trigger, _entry.Trigger)) continue;
                if (_list[i].Phase != _entry.Phase) continue;
                if (_list[i].Priority != _entry.Priority) continue;
                if (_list[i].Order != _entry.Order) continue;
                removeIndex = i;
                break;
            }

            if (removeIndex >= 0)
            {
                _list.RemoveAt(removeIndex);
            }

            var entry = _entry;
            var listWasEmpty = _list.Count == 0;
            _list = null;
            _entry = default;

            _lifecycle.OnUnregistered(_key, entry.Trigger);

            if (listWasEmpty && _subscription != null)
            {
                _unsubscribe(_key, _subscription);
            }
        }
    }
}

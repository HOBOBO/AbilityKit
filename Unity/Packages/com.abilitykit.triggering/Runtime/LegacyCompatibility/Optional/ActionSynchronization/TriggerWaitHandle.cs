using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 旧版动作信号句柄，仅作兼容保留；不提供阻塞等待或公开的等待者注册。
    /// </summary>
    public sealed class TriggerWaitHandle
    {
        private readonly Queue<Action> _waitingActions = new();
        private bool _isSignaled;
        private readonly bool _autoReset;

        /// <summary>
        /// 当前是否已设置信号。
        /// </summary>
        public bool IsSignaled => _isSignaled;

        /// <summary>
        /// 创建未置位的信号句柄；自动重置模式下，Set 在正常返回前清除信号。
        /// </summary>
        public TriggerWaitHandle(bool autoReset = true)
        {
            _autoReset = autoReset;
            _isSignaled = false;
        }

        /// <summary>
        /// 已置位时立即返回且不消耗信号；未置位时抛出 NotSupportedException，不阻塞。
        /// </summary>
        public void WaitOne()
        {
            if (_isSignaled)
            {
                return;
            }

            throw new NotSupportedException("TriggerWaitHandle.WaitOne is not supported in the mainline scheduler. Use Signal/Set to release waiting actions, or move coordination into a coroutine/state-machine based executor.");
        }

        /// <summary>
        /// 设置信号；自动重置模式下在正常返回前清除信号。
        /// </summary>
        public void Set()
        {
            _isSignaled = true;
            lock (_waitingActions)
            {
                foreach (var continuation in _waitingActions)
                {
                    continuation?.Invoke();
                }
                _waitingActions.Clear();
            }

            if (_autoReset)
            {
                _isSignaled = false;
            }
        }

        /// <summary>
        /// 清除信号；手动和自动重置模式都可以调用。
        /// </summary>
        public void Reset()
        {
            _isSignaled = false;
        }
    }
}

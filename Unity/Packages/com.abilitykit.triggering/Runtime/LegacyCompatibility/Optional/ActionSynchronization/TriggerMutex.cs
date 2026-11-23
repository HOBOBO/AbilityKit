using System;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 旧版非阻塞动作互斥句柄，仅作兼容保留；调用方传入的实例 ID 用来标记所有权。
    /// </summary>
    public sealed class TriggerMutex : IDisposable
    {
        private readonly string _mutexId;
        private readonly object _lockObj = new();
        private int _ownerInstanceId = -1;
        private bool _isHeld;

        /// <summary>
        /// 此句柄的标识；同名的不同句柄不共享状态。
        /// </summary>
        public string MutexId => _mutexId;
        /// <summary>
        /// 当前是否已被某个动作实例占用。
        /// </summary>
        public bool IsHeld => _isHeld;
        /// <summary>
        /// 当前持有者 ID；未占用时为 -1。
        /// </summary>
        public int OwnerInstanceId => _ownerInstanceId;

        /// <summary>
        /// 创建尚未被占用的互斥句柄。
        /// </summary>
        public TriggerMutex(string mutexId)
        {
            _mutexId = mutexId ?? throw new ArgumentNullException(nameof(mutexId));
        }

        /// <summary>
        /// 尝试取得所有权；已占用时返回 false，不等待，也不重入。
        /// </summary>
        public bool Enter(int instanceId)
        {
            lock (_lockObj)
            {
                if (_isHeld)
                {
                    return false;
                }

                _isHeld = true;
                _ownerInstanceId = instanceId;
                return true;
            }
        }

        /// <summary>
        /// 仅在指定实例为当前持有者时释放占用。
        /// </summary>
        public void Leave(int instanceId)
        {
            lock (_lockObj)
            {
                if (_isHeld && _ownerInstanceId == instanceId)
                {
                    _isHeld = false;
                    _ownerInstanceId = -1;
                }
            }
        }

        /// <summary>
        /// 释放当前占用，不将此句柄标记为永久停用。
        /// </summary>
        public void Dispose()
        {
            Leave(_ownerInstanceId);
        }
    }
}

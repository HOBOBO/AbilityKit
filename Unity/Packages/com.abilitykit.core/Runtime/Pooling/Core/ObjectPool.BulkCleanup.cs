using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace AbilityKit.Core.Pooling
{
    /// <remarks>
    /// 批量清理先在同一把池锁内提交选中的空闲对象，再在锁外执行销毁回调。
    /// 这部分与单个对象的租用、归还及预热共用同一份池状态。
    /// </remarks>
    public sealed partial class ObjectPool<T>
    {
        /// <summary>
        /// 清空当前空闲对象，默认仅丢弃引用；销毁式清空逐一调用销毁回调，不影响仍在借出的对象。
        /// </summary>
        /// <param name="destroy">是否对被移除的空闲对象执行销毁回调。</param>
        /// <remarks>即使销毁回调失败也处理所有已移除对象；一个异常原样传播，多个异常聚合，不回滚移除。</remarks>
        public void Clear(bool destroy = false)
        {
            List<T>? elements = null;
            lock (_syncRoot)
            {
                if (!destroy)
                {
                    _droppedInactiveCount += _stack.Count;
                    _stack.Clear();
                    _inactiveSet?.Clear();
                    return;
                }

                elements = DetachForDestructionUnsafe(_stack.Count, clearReason: true);
            }

            DestroyElements(elements);
        }

        /// <summary>
        /// 按构造时的策略裁剪空闲对象；NeverTrim 为真时不执行。
        /// </summary>
        /// <returns>成功处理的空闲移除数量，销毁失败时抛出异常而不返回数量。</returns>
        public int Trim()
        {
            return Trim(_trimPolicy);
        }

        /// <summary>
        /// 按指定策略裁剪空闲对象；NeverTrim 为真时不执行，不创建对象以补足保留目标。
        /// </summary>
        /// <param name="policy">决定空闲保留目标的策略。</param>
        /// <returns>移除的空闲对象数量；禁止常规裁剪时返回零。</returns>
        public int Trim(PoolTrimPolicy policy)
        {
            if (_neverTrim) return 0;
            return TrimCore(policy);
        }

        /// <summary>
        /// 按指定策略强制裁剪空闲对象，忽略 NeverTrim；销毁异常传播，不撤销已提交的移除。
        /// </summary>
        /// <param name="policy">决定空闲保留目标的策略。</param>
        /// <returns>成功处理的空闲移除数量，销毁失败时抛出异常而不返回数量。</returns>
        public int ForceTrim(PoolTrimPolicy policy)
        {
            return TrimCore(policy);
        }

        private int TrimCore(PoolTrimPolicy policy)
        {
            var targetInactiveCount = policy.ResolveTargetInactiveCount(_defaultCapacity);
            List<T>? elements;
            lock (_syncRoot)
            {
                elements = DetachForDestructionUnsafe(
                    System.Math.Max(0, _stack.Count - targetInactiveCount),
                    clearReason: false);
            }

            DestroyElements(elements);
            return elements?.Count ?? 0;
        }

        private List<T>? DetachForDestructionUnsafe(int count, bool clearReason)
        {
            if (count <= 0) return null;

            var elements = new List<T>(count);
            for (var index = 0; index < count; index++)
            {
                var element = _stack.Pop();
                _inactiveSet?.Remove(element);
                BeginTransitionUnsafe(element);
                elements.Add(element);
            }

            if (clearReason) _clearDestroyCount += elements.Count;
            else _trimDestroyCount += elements.Count;
            _destroyedTotal += elements.Count;
            return elements;
        }

        private void DestroyElements(List<T>? elements)
        {
            if (elements == null) return;

            List<Exception>? exceptions = null;
            for (var index = 0; index < elements.Count; index++)
            {
                try
                {
                    InvokeDestroyCallbacks(elements[index]);
                }
                catch (Exception exception)
                {
                    if (exceptions == null) exceptions = new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            ThrowCapturedExceptions(exceptions);
        }

        private static void ThrowCapturedExceptions(List<Exception>? exceptions)
        {
            if (exceptions == null || exceptions.Count == 0) return;
            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                return;
            }

            throw new AggregateException(exceptions);
        }

    }
}

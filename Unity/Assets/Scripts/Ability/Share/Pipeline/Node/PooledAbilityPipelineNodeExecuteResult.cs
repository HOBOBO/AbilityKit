using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability
{
    public sealed class PooledAbilityPipelineNodeExecuteResult : AbilityPipelineNodeExecuteResult, IDisposable, IPoolable
    {
        private static readonly ObjectPool<PooledAbilityPipelineNodeExecuteResult> s_pool = Pools.GetPool(
            createFunc: () => new PooledAbilityPipelineNodeExecuteResult(),
            onRelease: r => r.ResetInternal(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<List<string>> s_portsPool = Pools.GetPool(
            createFunc: () => new List<string>(2),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private static readonly ObjectPool<Dictionary<string, object>> s_outputDataPool = Pools.GetPool(
            createFunc: () => new Dictionary<string, object>(StringComparer.Ordinal),
            onRelease: dict => dict.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private bool _fromPool;

        private PooledAbilityPipelineNodeExecuteResult()
        {
            _fromPool = false;
        }

        public static PooledAbilityPipelineNodeExecuteResult Rent()
        {
            return s_pool.Get();
        }

        public void EnsureActiveOutputPorts()
        {
            if (ActiveOutputPorts == null)
            {
                ActiveOutputPorts = s_portsPool.Get();
            }
        }

        public void EnsureOutputData()
        {
            if (OutputData == null)
            {
                OutputData = s_outputDataPool.Get();
            }
        }

        public void Dispose()
        {
            if (!_fromPool) return;
            s_pool.Release(this);
        }

        public void OnPoolGet()
        {
            _fromPool = true;
        }

        public void OnPoolRelease()
        {
            ResetInternal();
        }

        public void OnPoolDestroy()
        {
            ResetInternal();
        }

        private void ResetInternal()
        {
            IsCompleted = false;

            if (ActiveOutputPorts != null)
            {
                s_portsPool.Release(ActiveOutputPorts);
                ActiveOutputPorts = null;
            }

            if (OutputData != null)
            {
                s_outputDataPool.Release(OutputData);
                OutputData = null;
            }
        }
    }
}

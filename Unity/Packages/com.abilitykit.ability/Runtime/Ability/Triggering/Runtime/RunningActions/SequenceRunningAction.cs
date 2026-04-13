using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Pool;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class SequenceRunningAction : IRunningAction
    {
        private static readonly ObjectPool<List<IRunningAction>> _listPool = Pools.GetPool(
            createFunc: () => new List<IRunningAction>(4),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private readonly List<IRunningAction> _steps;
        private int _index;
        private bool _done;
        private bool _disposed;

        public SequenceRunningAction(IReadOnlyList<IRunningAction> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = _listPool.Get();
            if (_steps.Capacity < steps.Count) _steps.Capacity = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null) _steps.Add(steps[i]);
            }
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            while (_index < _steps.Count)
            {
                var cur = _steps[_index];
                if (cur == null)
                {
                    _index++;
                    continue;
                }

                if (!cur.IsDone)
                {
                    cur.Tick(deltaTime);
                }

                if (cur.IsDone)
                {
                    TryDispose(cur);
                    _index++;
                    continue;
                }

                return;
            }

            _done = true;
        }

        public void Cancel()
        {
            if (_done) return;

            for (int i = _index; i < _steps.Count; i++)
            {
                var a = _steps[i];
                if (a == null) continue;
                a.Cancel();
                TryDispose(a);
            }

            _done = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _steps.Count; i++)
            {
                TryDispose(_steps[i]);
            }

            _listPool.Release(_steps);
        }

        private static void TryDispose(IRunningAction a)
        {
            if (a == null) return;
            try
            {
                a.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[SequenceRunningAction] step dispose failed");
            }
        }
    }
}

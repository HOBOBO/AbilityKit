using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class ParallelRunningAction : IRunningAction
    {
        private static readonly ObjectPool<List<IRunningAction>> _listPool = Pools.GetPool(
            createFunc: () => new List<IRunningAction>(4),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private readonly List<IRunningAction> _actions;
        private bool _done;
        private bool _disposed;

        public ParallelRunningAction(IReadOnlyList<IRunningAction> actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            _actions = _listPool.Get();
            if (_actions.Capacity < actions.Count) _actions.Capacity = actions.Count;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null) _actions.Add(actions[i]);
            }
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            var alive = 0;
            for (int i = 0; i < _actions.Count; i++)
            {
                var a = _actions[i];
                if (a == null) continue;

                if (!a.IsDone)
                {
                    a.Tick(deltaTime);
                }

                if (a.IsDone)
                {
                    TryDispose(a);
                    _actions[i] = null;
                }
                else
                {
                    alive++;
                }
            }

            if (alive == 0) _done = true;
        }

        public void Cancel()
        {
            if (_done) return;

            for (int i = 0; i < _actions.Count; i++)
            {
                var a = _actions[i];
                if (a == null) continue;
                a.Cancel();
                TryDispose(a);
                _actions[i] = null;
            }

            _done = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _actions.Count; i++)
            {
                TryDispose(_actions[i]);
            }

            _listPool.Release(_actions);
        }

        private static void TryDispose(IRunningAction a)
        {
            if (a == null) return;
            try
            {
                a.Dispose();
            }
            catch
            {
            }
        }
    }
}

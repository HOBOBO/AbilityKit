using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggerActionRunner : ITriggerActionRunner
    {
        private struct Entry
        {
            public IRunningAction Action;
            public object Owner;
            public long OwnerKey;
        }

        private readonly List<Entry> _running = new List<Entry>(64);

        public int RunningCount => _running.Count;

        public void Add(IRunningAction action, object owner = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _running.Add(new Entry { Action = action, Owner = owner, OwnerKey = 0 });
        }

        public void Add(IRunningAction action, long ownerKey)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _running.Add(new Entry { Action = action, Owner = null, OwnerKey = ownerKey });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                var a = e.Action;
                if (a == null)
                {
                    _running.RemoveAt(i);
                    continue;
                }

                if (a.IsDone)
                {
                    TryDispose(a);
                    _running.RemoveAt(i);
                    continue;
                }

                a.Tick(deltaTime);

                if (a.IsDone)
                {
                    TryDispose(a);
                    _running.RemoveAt(i);
                }
            }
        }

        public int CancelByOwner(object owner)
        {
            if (owner == null) return 0;
            var count = 0;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (!ReferenceEquals(e.Owner, owner)) continue;

                var a = e.Action;
                if (a != null)
                {
                    a.Cancel();
                    TryDispose(a);
                }

                _running.RemoveAt(i);
                count++;
            }

            return count;
        }

        public int CancelByOwnerKey(long ownerKey)
        {
            if (ownerKey == 0) return 0;
            var count = 0;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.OwnerKey != ownerKey) continue;

                var a = e.Action;
                if (a != null)
                {
                    a.Cancel();
                    TryDispose(a);
                }

                _running.RemoveAt(i);
                count++;
            }

            return count;
        }

        public int CancelAll()
        {
            var count = 0;
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var a = _running[i].Action;
                if (a != null)
                {
                    a.Cancel();
                    TryDispose(a);
                    count++;
                }
            }

            _running.Clear();
            return count;
        }

        private static void TryDispose(IRunningAction action)
        {
            try
            {
                action.Dispose();
            }
            catch
            {
            }
        }
    }
}

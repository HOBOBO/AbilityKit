using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowEventQueue<TEvent>
    {
        private readonly Queue<TEvent> _queue = new Queue<TEvent>();

        public int Count => _queue.Count;

        public void Enqueue(TEvent e)
        {
            _queue.Enqueue(e);
        }

        public bool TryDequeue(out TEvent e)
        {
            if (_queue.Count > 0)
            {
                e = _queue.Dequeue();
                return true;
            }

            e = default;
            return false;
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}

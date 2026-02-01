using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleLocalInputQueue : ILocalInputSource<LocalPlayerInputEvent[]>
    {
        private readonly Queue<LocalPlayerInputEvent[]> _queue;
        private readonly List<LocalPlayerInputEvent> _buffer;
        private int _localFrame;

        public BattleLocalInputQueue(int initialCapacity = 32)
        {
            if (initialCapacity <= 0) initialCapacity = 4;
            _queue = new Queue<LocalPlayerInputEvent[]>(initialCapacity);
            _buffer = new List<LocalPlayerInputEvent>(8);
            _localFrame = 0;
        }

        public int LocalFrame => _localFrame;

        public void Dispose()
        {
            Clear();
        }

        public void Clear()
        {
            _queue.Clear();
            _buffer.Clear();
            _localFrame = 0;
        }

        public void Enqueue(in LocalPlayerInputEvent evt)
        {
            _buffer.Add(evt);
        }

        public void Flush()
        {
            var arr = _buffer.Count == 0 ? Array.Empty<LocalPlayerInputEvent>() : _buffer.ToArray();
            _queue.Enqueue(arr);
            _buffer.Clear();
            _localFrame++;
        }

        public bool TryDequeue(out LocalPlayerInputEvent[] input)
        {
            if (_queue.Count > 0)
            {
                input = _queue.Dequeue();
                return true;
            }

            input = null;
            return false;
        }
    }
}

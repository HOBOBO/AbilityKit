using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 任务列表。
    /// 内部使用的无 GC 列表实现。
    /// </summary>
    internal class TaskList
    {
        private ScheduledTaskBase[] _items;
        private int _count;

        public int Count => _count;

        public TaskList(int capacity)
        {
            _items = new ScheduledTaskBase[capacity];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ScheduledTaskBase task)
        {
            if (_count >= _items.Length)
            {
                var newItems = new ScheduledTaskBase[_items.Length * 2];
                Array.Copy(_items, newItems, _count);
                _items = newItems;
            }
            _items[_count++] = task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            _count--;
            if (index < _count)
            {
                _items[index] = _items[_count];
            }
            _items[_count] = null;
        }

        public ScheduledTaskBase this[int index] => _items[index];
    }
}

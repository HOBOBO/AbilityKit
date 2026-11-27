#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 维护详细作用域的开始线程、嵌套关系和结束顺序，由 Profiler 的聚合锁保护。
    /// </summary>
    internal sealed class ProfilerScopeTracker
    {
        /// <summary>
        /// 标识一次采样。令牌副本共享完成状态，轻量采集也可原子地认领完成权。
        /// </summary>
        internal sealed class Capture
        {
            private int _completed;

            internal Capture(string name, long startTimestamp, long contextId, long generation, int threadId = 0)
            {
                Name = name;
                StartTimestamp = startTimestamp;
                ContextId = contextId;
                Generation = generation;
                ThreadId = threadId;
            }

            internal string Name { get; }
            internal long StartTimestamp { get; }
            internal long ContextId { get; }
            internal long Generation { get; }
            internal int ThreadId { get; }
            internal bool IsDetailed => ThreadId != 0;
            internal bool IsPending => Volatile.Read(ref _completed) == 0;

            internal bool TryComplete() => Interlocked.Exchange(ref _completed, 1) == 0;
        }

        /// <summary>
        /// 保存详细作用域的树节点及已计入的直接子项耗时，不随令牌副本流转。
        /// </summary>
        internal struct Frame
        {
            internal Frame(Capture capture, FlameNode node)
            {
                Capture = capture;
                Node = node;
                ChildNanoseconds = 0L;
            }

            internal Capture Capture { get; }
            internal FlameNode Node { get; }
            internal long ChildNanoseconds { get; set; }
        }

        private readonly Dictionary<int, List<Frame>> _stacks = new Dictionary<int, List<Frame>>();

        internal FlameNode? GetCurrentNode(int threadId)
        {
            return _stacks.TryGetValue(threadId, out var stack) && stack.Count > 0
                ? stack[stack.Count - 1].Node
                : null;
        }

        internal void Push(Capture capture, FlameNode node)
        {
            if (!_stacks.TryGetValue(capture.ThreadId, out var stack))
            {
                stack = new List<Frame>();
                _stacks.Add(capture.ThreadId, stack);
            }

            stack.Add(new Frame(capture, node));
        }

        internal void AddChildDuration(int threadId, long nanoseconds)
        {
            if (_stacks.TryGetValue(threadId, out var stack))
            {
                AddToParent(stack, nanoseconds);
            }
        }

        internal bool TryClose(Capture capture, long timestamp, long logicalFrame, Action<Frame, long, long> record)
        {
            if (!_stacks.TryGetValue(capture.ThreadId, out var stack))
            {
                return false;
            }

            var target = stack.Count - 1;
            while (target >= 0 && !ReferenceEquals(stack[target].Capture, capture))
            {
                target--;
            }

            if (target < 0)
            {
                return false;
            }

            /*
             * 乱序结束沿开始线程的栈从内到外收尾，所有被收尾的作用域使用同一个结束时刻。
             * 普通按序结束命中栈顶，不需要搜索或构造临时集合。
             */
            for (var index = stack.Count - 1; index >= target; index--)
            {
                var frame = stack[index];
                stack.RemoveAt(index);
                frame.Capture.TryComplete();
                var elapsed = EditorProfiler.TicksToNanoseconds(timestamp - frame.Capture.StartTimestamp);
                AddToParent(stack, elapsed);
                record(frame, elapsed, logicalFrame);
            }

            return true;
        }

        internal void Clear() => _stacks.Clear();

        private static void AddToParent(List<Frame> stack, long nanoseconds)
        {
            var index = stack.Count - 1;
            if (index >= 0)
            {
                var parent = stack[index];
                parent.ChildNanoseconds += nanoseconds;
                stack[index] = parent;
            }
        }
    }
}

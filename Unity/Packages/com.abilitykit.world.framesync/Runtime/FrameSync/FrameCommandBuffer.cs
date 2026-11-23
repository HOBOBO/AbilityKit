using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.Core.Collections;

namespace AbilityKit.Ability.FrameSync
{
    /// <summary>
    /// 面向锁步/帧同步运行时的通用按帧索引命令存储。
    /// 只负责帧分桶、窗口参数、复制和显式裁剪，不证明输入已结算或帧已齐备。
    /// 内部同步保护集合，不复制命令引用，也不保护调用者提供的目标列表或比较器。
    /// </summary>
    public sealed class FrameCommandBuffer<TKey, TCommand>
        where TKey : notnull
    {
        private static readonly IReadOnlyDictionary<TKey, TCommand> EmptyFrameCommands =
            new ReadOnlyDictionary<TKey, TCommand>(new Dictionary<TKey, TCommand>(0));

        private readonly object _sync = new object();
        private readonly Dictionary<int, Dictionary<TKey, TCommand>> _frames = new Dictionary<int, Dictionary<TKey, TCommand>>();
        private readonly SortedIntSet _frameNumbers;
        private readonly IComparer<TCommand>? _commandComparer;
        private readonly Stack<Dictionary<TKey, TCommand>> _frameCommandPool = new Stack<Dictionary<TKey, TCommand>>();
        private int _oldestRetainedFrame;
        private int _retainedFrameWindow;
        private int _latestFrame;

        /// <summary>
        /// 创建缓冲；窗口默认 120，最小归一到 1。提交不自动裁剪；指定比较器才会对复制结果排序。
        /// </summary>
        public FrameCommandBuffer(int retainedFrameWindow = 120, IComparer<TCommand>? commandComparer = null)
        {
            _retainedFrameWindow = retainedFrameWindow < 1 ? 1 : retainedFrameWindow;
            _frameNumbers = new SortedIntSet(_retainedFrameWindow);
            _commandComparer = commandComparer;
        }

        /// <summary>
        /// 返回提交与裁剪使用的帧号下界；不一定实际存有该帧。
        /// </summary>
        public int OldestRetainedFrame
        {
            get { lock (_sync) return _oldestRetainedFrame; }
        }

        /// <summary>
        /// 返回裁剪时使用的帧号跨度，不是命令或分桶的硬容量。
        /// </summary>
        public int RetainedFrameWindow
        {
            get { lock (_sync) return _retainedFrameWindow; }
        }

        /// <summary>
        /// 返回清空以来写入的最大实际帧号；清空后为 0，裁剪不回退该值。
        /// </summary>
        public int LatestFrame
        {
            get { lock (_sync) return _latestFrame; }
        }

        /// <summary>
        /// 清空已保留的命令，并将下界和最大帧号重置为 0；保留窗口配置。
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                foreach (var kv in _frames)
                {
                    RetireFrameCommands(kv.Value);
                }
                _frames.Clear();
                _frameNumbers.Clear();
                _oldestRetainedFrame = 0;
                _latestFrame = 0;
            }
        }

        /// <summary>
        /// 设置窗口跨度，最小为 1；仅在锚点大于 0 时按该锚点裁剪一次，不回退下界。
        /// </summary>
        public void SetRetainedFrameWindow(int frames, int anchorFrame = 0)
        {
            lock (_sync)
            {
                _retainedFrameWindow = frames < 1 ? 1 : frames;
                if (anchorFrame > 0)
                {
                    TrimBeforeLocked(Math.Max(_oldestRetainedFrame, anchorFrame - _retainedFrameWindow));
                }
            }
        }

        /// <summary>
        /// 提交命令的兼容别名，与 <c>SubmitCommand</c> 的替换和下界规则相同。
        /// </summary>
        public void SetCommand(int frame, TKey key, in TCommand command)
        {
            SubmitCommand(frame, key, in command);
        }

        /// <summary>
        /// 写入指定帧和键；同键替换，下界之前的帧归并到下界。
        /// 不适合把必须保持原帧号的权威结算帧直接写入本接口。
        /// </summary>
        public void SubmitCommand(int frame, TKey key, in TCommand command)
        {
            lock (_sync)
            {
                if (frame < _oldestRetainedFrame)
                {
                    frame = _oldestRetainedFrame;
                }

                if (!_frames.TryGetValue(frame, out var commands))
                {
                    commands = _frameCommandPool.Count > 0
                        ? _frameCommandPool.Pop()
                        : new Dictionary<TKey, TCommand>();
                    _frames[frame] = commands;
                    _frameNumbers.Add(frame);
                }

                commands[key] = command;
                if (frame > _latestFrame) _latestFrame = frame;
            }
        }

        /// <summary>
        /// 查询精确的帧号和键；未命中返回失败并输出默认值。
        /// </summary>
        public bool TryGetCommand(int frame, TKey key, out TCommand command)
        {
            lock (_sync)
            {
                command = default!;
                return _frames.TryGetValue(frame, out var commands) && commands.TryGetValue(key, out command);
            }
        }

        /// <summary>
        /// 命中时返回独立字典副本，否则返回共享只读空集合；不表示收到空结算帧。
        /// </summary>
        public IReadOnlyDictionary<TKey, TCommand> GetFrameCommandsOrEmpty(int frame)
        {
            lock (_sync)
            {
                return _frames.TryGetValue(frame, out var commands)
                    ? new Dictionary<TKey, TCommand>(commands)
                    : EmptyFrameCommands;
            }
        }

        /// <summary>
        /// 命中时返回独立字典副本，未命中时返回失败和共享只读空集合；均不复制命令对象。
        /// </summary>
        public bool TryGetFrameCommands(int frame, out IReadOnlyDictionary<TKey, TCommand> commands)
        {
            lock (_sync)
            {
                if (_frames.TryGetValue(frame, out var frameCommands))
                {
                    commands = new Dictionary<TKey, TCommand>(frameCommands);
                    return true;
                }

                commands = EmptyFrameCommands;
                return false;
            }
        }

        /// <summary>
        /// 清空目标列表并复制指定帧的命令；若指定比较器，在锁外排序。需要确定顺序时应提供完整的排序规则。
        /// </summary>
        public int CopyFrameCommands(int frame, List<TCommand> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            lock (_sync)
            {
                if (!_frames.TryGetValue(frame, out var commands))
                {
                    return 0;
                }

                foreach (var kv in commands)
                {
                    destination.Add(kv.Value);
                }
            }

            if (_commandComparer != null)
            {
                destination.Sort(_commandComparer);
            }

            return destination.Count;
        }

        /// <summary>
        /// 清空目标列表并以升序复制分桶帧号；范围含起始、不含结束。
        /// </summary>
        public int CopyRetainedFrameNumbers(List<int> destination, int startFrameInclusive = 0, int endFrameExclusive = int.MaxValue)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            lock (_sync)
            {
                var endIndex = _frameNumbers.LowerBound(endFrameExclusive);
                for (var index = _frameNumbers.LowerBound(startFrameInclusive); index < endIndex; index++)
                {
                    destination.Add(_frameNumbers[index]);
                }
            }

            return destination.Count;
        }

        /// <summary>
        /// 移除严格早于指定帧的分桶，并前推写入下界；不会将下界往回移。
        /// </summary>
        public void TrimBefore(int frame)
        {
            lock (_sync)
            {
                TrimBeforeLocked(frame);
            }
        }

        /// <summary>
        /// 以当前帧减去窗口跨度为下界裁剪；保留下界帧，不限制高于当前帧的分桶。
        /// </summary>
        public void TrimToWindow(int currentFrame)
        {
            lock (_sync)
            {
                TrimBeforeLocked(Math.Max(_oldestRetainedFrame, currentFrame - _retainedFrameWindow));
            }
        }

        private void TrimBeforeLocked(int frame)
        {
            if (frame <= _oldestRetainedFrame)
            {
                return;
            }

            var removeCount = _frameNumbers.LowerBound(frame);
            for (var index = 0; index < removeCount; index++)
            {
                if (_frames.Remove(_frameNumbers[index], out var retired))
                {
                    RetireFrameCommands(retired);
                }
            }

            if (removeCount > 0) _frameNumbers.RemoveRange(0, removeCount);

            _oldestRetainedFrame = frame;
        }

        /*
         * 调用方须持有集合锁。读取返回独立副本，回收分桶不会影响已有读者。
         */
        private void RetireFrameCommands(Dictionary<TKey, TCommand> commands)
        {
            commands.Clear();
            _frameCommandPool.Push(commands);
        }
    }
}

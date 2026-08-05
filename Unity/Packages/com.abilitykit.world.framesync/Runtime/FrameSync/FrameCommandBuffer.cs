#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Ability.FrameSync
{
    /// <summary>
    /// 面向锁步/帧同步运行时的通用按帧索引命令存储。
    /// 负责可复用的帧分桶、保留窗口、确定性复制和裁剪机制，
    /// 将特定领域的最新输入或边沿触发语义留给调用方处理。
    /// </summary>
    public sealed class FrameCommandBuffer<TKey, TCommand>
        where TKey : notnull
    {
        private static readonly IReadOnlyDictionary<TKey, TCommand> EmptyFrameCommands = new Dictionary<TKey, TCommand>(0);

        private readonly Dictionary<int, Dictionary<TKey, TCommand>> _frames = new Dictionary<int, Dictionary<TKey, TCommand>>();
        private readonly IComparer<TCommand>? _commandComparer;
        private int _oldestRetainedFrame;
        private int _retainedFrameWindow;
        private int _latestFrame;

        public FrameCommandBuffer(int retainedFrameWindow = 120, IComparer<TCommand>? commandComparer = null)
        {
            _retainedFrameWindow = retainedFrameWindow < 1 ? 1 : retainedFrameWindow;
            _commandComparer = commandComparer;
        }

        public int OldestRetainedFrame => _oldestRetainedFrame;

        public int RetainedFrameWindow => _retainedFrameWindow;

        public int LatestFrame => Volatile.Read(ref _latestFrame);

        public void Clear()
        {
            _frames.Clear();
            _oldestRetainedFrame = 0;
            Volatile.Write(ref _latestFrame, 0);
        }

        public void SetRetainedFrameWindow(int frames, int anchorFrame = 0)
        {
            _retainedFrameWindow = frames < 1 ? 1 : frames;
            if (anchorFrame > 0)
            {
                TrimBefore(Math.Max(_oldestRetainedFrame, anchorFrame - _retainedFrameWindow));
            }
        }

        public void SetCommand(int frame, TKey key, in TCommand command)
        {
            SubmitCommand(frame, key, in command);
        }

        public void SubmitCommand(int frame, TKey key, in TCommand command)
        {
            if (frame < _oldestRetainedFrame)
            {
                frame = _oldestRetainedFrame;
            }

            if (!_frames.TryGetValue(frame, out var commands))
            {
                commands = new Dictionary<TKey, TCommand>();
                _frames[frame] = commands;
            }

            commands[key] = command;

            // Atomically advance _latestFrame. Uses CompareExchange loop to avoid the
            // check-then-set race when multiple callers submit commands concurrently.
            var current = Volatile.Read(ref _latestFrame);
            while (frame > current)
            {
                var original = Interlocked.CompareExchange(ref _latestFrame, frame, current);
                if (original == current) break;
                current = original;
            }
        }

        public bool TryGetCommand(int frame, TKey key, out TCommand command)
        {
            command = default!;
            return _frames.TryGetValue(frame, out var commands) && commands.TryGetValue(key, out command);
        }

        public IReadOnlyDictionary<TKey, TCommand> GetFrameCommandsOrEmpty(int frame)
        {
            return _frames.TryGetValue(frame, out var commands) ? commands : EmptyFrameCommands;
        }

        public bool TryGetFrameCommands(int frame, out IReadOnlyDictionary<TKey, TCommand> commands)
        {
            if (_frames.TryGetValue(frame, out var frameCommands))
            {
                commands = frameCommands;
                return true;
            }

            commands = EmptyFrameCommands;
            return false;
        }

        public int CopyFrameCommands(int frame, List<TCommand> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (!_frames.TryGetValue(frame, out var commands))
            {
                return 0;
            }

            foreach (var kv in commands)
            {
                destination.Add(kv.Value);
            }

            if (_commandComparer != null)
            {
                destination.Sort(_commandComparer);
            }

            return destination.Count;
        }

        public int CopyRetainedFrameNumbers(List<int> destination, int startFrameInclusive = 0, int endFrameExclusive = int.MaxValue)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            foreach (var kv in _frames)
            {
                if (kv.Key >= startFrameInclusive && kv.Key < endFrameExclusive)
                {
                    destination.Add(kv.Key);
                }
            }

            destination.Sort();
            return destination.Count;
        }

        public void TrimBefore(int frame)
        {
            if (frame <= _oldestRetainedFrame)
            {
                return;
            }

            var removed = new List<int>();
            foreach (var kv in _frames)
            {
                if (kv.Key < frame)
                {
                    removed.Add(kv.Key);
                }
            }

            for (var i = 0; i < removed.Count; i++)
            {
                _frames.Remove(removed[i]);
            }

            _oldestRetainedFrame = frame;
        }

        public void TrimToWindow(int currentFrame)
        {
            TrimBefore(Math.Max(_oldestRetainedFrame, currentFrame - _retainedFrameWindow));
        }
    }
}

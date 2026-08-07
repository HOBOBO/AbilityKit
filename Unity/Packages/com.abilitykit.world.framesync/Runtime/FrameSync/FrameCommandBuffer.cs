#pragma warning disable CS1591

using System;
using System.Collections.Generic;

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

        private readonly object _sync = new object();
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

        public int OldestRetainedFrame
        {
            get { lock (_sync) return _oldestRetainedFrame; }
        }

        public int RetainedFrameWindow
        {
            get { lock (_sync) return _retainedFrameWindow; }
        }

        public int LatestFrame
        {
            get { lock (_sync) return _latestFrame; }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _frames.Clear();
                _oldestRetainedFrame = 0;
                _latestFrame = 0;
            }
        }

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

        public void SetCommand(int frame, TKey key, in TCommand command)
        {
            SubmitCommand(frame, key, in command);
        }

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
                    commands = new Dictionary<TKey, TCommand>();
                    _frames[frame] = commands;
                }

                commands[key] = command;
                if (frame > _latestFrame) _latestFrame = frame;
            }
        }

        public bool TryGetCommand(int frame, TKey key, out TCommand command)
        {
            lock (_sync)
            {
                command = default!;
                return _frames.TryGetValue(frame, out var commands) && commands.TryGetValue(key, out command);
            }
        }

        public IReadOnlyDictionary<TKey, TCommand> GetFrameCommandsOrEmpty(int frame)
        {
            lock (_sync)
            {
                return _frames.TryGetValue(frame, out var commands)
                    ? new Dictionary<TKey, TCommand>(commands)
                    : EmptyFrameCommands;
            }
        }

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

        public int CopyRetainedFrameNumbers(List<int> destination, int startFrameInclusive = 0, int endFrameExclusive = int.MaxValue)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            lock (_sync)
            {
                foreach (var kv in _frames)
                {
                    if (kv.Key >= startFrameInclusive && kv.Key < endFrameExclusive)
                    {
                        destination.Add(kv.Key);
                    }
                }
            }

            destination.Sort();
            return destination.Count;
        }

        public void TrimBefore(int frame)
        {
            lock (_sync)
            {
                TrimBeforeLocked(frame);
            }
        }

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
    }
}

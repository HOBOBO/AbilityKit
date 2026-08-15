using System;
using System.Collections.Generic;
using AbilityKit.Core.Collections;

namespace AbilityKit.Ability.StateSync.Buffer
{

/// <summary>
/// 输入缓冲
/// 泛型版本，业务层提供具体的 IInputCommand 实现
/// </summary>
public sealed class InputBuffer<TInput> where TInput : class, IInputCommand
{
    private readonly Dictionary<int, TInput> _inputs = new();
    private readonly SortedIntSet _frames;
    private readonly int _maxBufferSize;
    private readonly int _localPlayerId;
    private readonly object _lock = new();

    public int LocalPlayerId => _localPlayerId;
    public int Count => _frames.Count;

    public InputBuffer(int localPlayerId, int maxBufferSize = 128)
    {
        if (maxBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
        _localPlayerId = localPlayerId;
        _maxBufferSize = maxBufferSize;
        _frames = new SortedIntSet(maxBufferSize);
    }

    public void Store(int frame, TInput input)
    {
        lock (_lock)
        {
            _inputs[frame] = input;
            _frames.Add(frame);
            TrimBuffer();
        }
    }

    public bool TryGet(int frame, out TInput input)
    {
        lock (_lock)
        {
            return _inputs.TryGetValue(frame, out input);
        }
    }

    public bool TryGetLocalInput(int frame, Func<TInput, bool> isLocal)
    {
        if (TryGet(frame, out var input))
        {
            return isLocal(input);
        }
        return false;
    }

    public bool PeekLocalInput(int frame, Func<TInput, bool> isLocal)
    {
        lock (_lock)
        {
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                int f = _frames[i];
                if (f <= frame && _inputs.TryGetValue(f, out var cmd) && isLocal(cmd))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public bool Contains(int frame)
    {
        lock (_lock)
        {
            return _inputs.ContainsKey(frame);
        }
    }

    public IReadOnlyList<TInput> GetInputsInRange(int startFrame, int endFrame)
    {
        lock (_lock)
        {
            var result = new List<TInput>();
            for (var index = _frames.LowerBound(startFrame); index < _frames.Count; index++)
            {
                var frame = _frames[index];
                if (frame > endFrame) break;
                if (frame >= startFrame && frame <= endFrame && _inputs.TryGetValue(frame, out var input))
                {
                    result.Add(input);
                }
            }
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _inputs.Clear();
            _frames.Clear();
        }
    }

    public void RemoveBefore(int frame)
    {
            lock (_lock)
            {
                var removeCount = _frames.LowerBound(frame);
                for (var index = 0; index < removeCount; index++)
                {
                    _inputs.Remove(_frames[index]);
                }

                if (removeCount > 0) _frames.RemoveRange(0, removeCount);
            }
    }

    private void TrimBuffer()
    {
        var removeCount = _frames.Count - _maxBufferSize;
        for (var index = 0; index < removeCount; index++)
        {
            _inputs.Remove(_frames[index]);
        }

        if (removeCount > 0) _frames.RemoveRange(0, removeCount);
    }

    public int GetInputCount()
    {
        lock (_lock)
        {
            return _frames.Count;
        }
    }

    public int GetLatestFrame()
    {
        lock (_lock)
        {
            return _frames.Count > 0 ? _frames[_frames.Count - 1] : -1;
        }
    }
}

}

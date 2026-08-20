using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Buffer
{

public interface IInputBuffer<TInput> where TInput : class, IInputCommand
{
    int LocalPlayerId { get; }
    int Count { get; }
    void Store(int frame, TInput input);
    bool TryGet(int frame, out TInput input);
    void Clear();
}

/// <summary>
/// 输入缓冲
/// 泛型版本，业务层提供具体的 IInputCommand 实现
/// </summary>
public sealed class InputBuffer<TInput> : IInputBuffer<TInput>, IBufferCapacityControl where TInput : class, IInputCommand
{
    private readonly IFrameIndexedBuffer<TInput> _storage;
    private readonly int _localPlayerId;
    private readonly object _lock = new();

    public int LocalPlayerId => _localPlayerId;
    public int Count => _storage.Count;
    public int Capacity => _storage.Capacity;

    public InputBuffer(int localPlayerId, int maxBufferSize = 128)
        : this(localPlayerId, new SparseFrameIndexedBuffer<TInput>(maxBufferSize))
    {
    }

    public InputBuffer(int localPlayerId, IFrameIndexedBuffer<TInput> storage)
    {
        _localPlayerId = localPlayerId;
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public void Store(int frame, TInput input)
    {
        lock (_lock)
        {
            _storage.Store(frame, input);
        }
    }

    public bool TryGet(int frame, out TInput input)
    {
        lock (_lock)
        {
            return _storage.TryGet(frame, out input);
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
            for (int i = _storage.Count - 1; i >= 0; i--)
            {
                int f = _storage.GetFrameAt(i);
                if (f <= frame && _storage.TryGet(f, out var cmd) && isLocal(cmd))
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
            return _storage.Contains(frame);
        }
    }

    public IReadOnlyList<TInput> GetInputsInRange(int startFrame, int endFrame)
    {
        lock (_lock)
        {
            var result = new List<TInput>();
            for (var index = _storage.LowerBound(startFrame); index < _storage.Count; index++)
            {
                var frame = _storage.GetFrameAt(index);
                if (frame > endFrame) break;
                if (_storage.TryGet(frame, out var input))
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
            _storage.Clear();
        }
    }

    public bool TrySetCapacity(int capacity)
    {
        if (capacity <= 0) return false;

        lock (_lock)
        {
            return _storage.TrySetCapacity(capacity);
        }
    }

    public void RemoveBefore(int frame)
    {
        lock (_lock)
        {
            _storage.RemoveBefore(frame);
        }
    }

    public int GetInputCount()
    {
        lock (_lock)
        {
            return _storage.Count;
        }
    }

    public int GetLatestFrame()
    {
        lock (_lock)
        {
            return _storage.Count > 0 ? _storage.GetFrameAt(_storage.Count - 1) : -1;
        }
    }
}

}

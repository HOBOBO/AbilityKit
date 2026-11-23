using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Buffer
{

/// <summary>
/// 按帧保存单条输入引用；宿主保证命令在保留期间不可变。
/// </summary>
public interface IInputBuffer<TInput> where TInput : class, IInputCommand
{
    /// <summary>
    /// 返回缓冲关联的本地玩家标识。
    /// </summary>
    int LocalPlayerId { get; }
    /// <summary>
    /// 返回保留输入的帧数。
    /// </summary>
    int Count { get; }
    /// <summary>
    /// 保存输入引用；同帧重复写入替换原命令。
    /// </summary>
    void Store(int frame, TInput input);
    /// <summary>
    /// 按帧读取输入引用；未命中时返回失败。
    /// </summary>
    bool TryGet(int frame, out TInput input);
    /// <summary>
    /// 清空已保存的输入。
    /// </summary>
    void Clear();
}

/// <summary>
/// 按帧存取业务输入的有界历史，不处理同帧批命令或网络确认。
/// </summary>
public sealed class InputBuffer<TInput> : IInputBuffer<TInput>, IBufferCapacityControl where TInput : class, IInputCommand
{
    private readonly IFrameIndexedBuffer<TInput> _storage;
    private readonly int _localPlayerId;
    private readonly object _lock = new();

    /// <summary>
    /// 返回构造时关联的玩家标识；写入不按此值自动过滤。
    /// </summary>
    public int LocalPlayerId => _localPlayerId;

    /// <summary>
    /// 返回读取时保留的帧数。
    /// </summary>
    public int Count => GetInputCount();

    /// <summary>
    /// 返回当前帧容量。
    /// </summary>
    public int Capacity
    {
        get { lock (_lock) return _storage.Capacity; }
    }

    /// <summary>
    /// 创建稀疏输入历史，默认保留 128 帧。
    /// </summary>
    public InputBuffer(int localPlayerId, int maxBufferSize = 128)
        : this(localPlayerId, new SparseFrameIndexedBuffer<TInput>(maxBufferSize))
    {
    }

    /// <summary>
    /// 使用指定底层缓冲；使用期间不得从外部并发操作该实例。
    /// </summary>
    public InputBuffer(int localPlayerId, IFrameIndexedBuffer<TInput> storage)
    {
        _localPlayerId = localPlayerId;
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// 按帧保存输入引用，不复制命令。
    /// </summary>
    public void Store(int frame, TInput input)
    {
        lock (_lock)
        {
            _storage.Store(frame, input);
        }
    }

    /// <summary>
    /// 按帧读取原输入引用；未命中时返回失败。
    /// </summary>
    public bool TryGet(int frame, out TInput input)
    {
        lock (_lock)
        {
            return _storage.TryGet(frame, out input);
        }
    }

    /// <summary>
    /// 判断该帧是否有符合谓词的输入；谓词在锁外执行。
    /// </summary>
    public bool TryGetLocalInput(int frame, Func<TInput, bool> isLocal)
    {
        if (TryGet(frame, out var input))
        {
            return isLocal(input);
        }
        return false;
    }

    /// <summary>
    /// 从给定帧向较早帧查找匹配项；谓词在锁内执行，不应阻塞或修改缓冲。
    /// </summary>
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

    /// <summary>
    /// 判断指定帧是否保留。
    /// </summary>
    public bool Contains(int frame)
    {
        lock (_lock)
        {
            return _storage.Contains(frame);
        }
    }

    /// <summary>
    /// 按帧号升序返回闭区间内的输入引用，结果列表与缓冲独立。
    /// </summary>
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

    /// <summary>
    /// 清空输入历史，不改变容量或关联玩家。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _storage.Clear();
        }
    }

    /// <summary>
    /// 尝试修改容量；非正容量直接拒绝，裁剪由底层缓冲处理。
    /// </summary>
    public bool TrySetCapacity(int capacity)
    {
        if (capacity <= 0) return false;

        lock (_lock)
        {
            return _storage.TrySetCapacity(capacity);
        }
    }

    /// <summary>
    /// 删除严格早于指定帧的输入。
    /// </summary>
    public void RemoveBefore(int frame)
    {
        lock (_lock)
        {
            _storage.RemoveBefore(frame);
        }
    }

    /// <summary>
    /// 返回保留输入的帧数，与 <see cref="Count"/> 等价。
    /// </summary>
    public int GetInputCount()
    {
        lock (_lock)
        {
            return _storage.Count;
        }
    }

    /// <summary>
    /// 返回最大保留帧号；空缓冲返回负一。
    /// </summary>
    public int GetLatestFrame()
    {
        lock (_lock)
        {
            return _storage.Count > 0 ? _storage.GetFrameAt(_storage.Count - 1) : -1;
        }
    }
}

}

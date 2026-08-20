using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Prediction
{

/// <summary>
/// 预测处理器接口
/// 通过槽位读写状态，完全通用
/// </summary>
public interface IPredictionHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 预测策略
    /// </summary>
    PredictionStrategy Strategy { get; }

    /// <summary>
    /// 需要的槽位模式（如 "position", "health", "cooldown.*"）
    /// </summary>
    IReadOnlyList<string> RequiredSlots { get; }

    /// <summary>
    /// 执行预测
    /// </summary>
    void Predict(IInputCommand input, StateSlots slots, Frame frame);

    /// <summary>
    /// 校验预测是否与服务器一致
    /// </summary>
    PredictionResult Validate(StateSlots predicted, StateSlots server);

    /// <summary>
    /// 应用服务器状态到当前状态
    /// </summary>
    void ApplyServerState(StateSlots server, StateSlots current);
}

/// <summary>
/// Copies mutable reference values when state slots are snapshotted.
/// Value types and strings are copied by the default StateSlots policy.
/// </summary>
public interface IStateSlotValueCloner
{
    /// <summary>Returns an independent value with the same runtime type.</summary>
    object Clone(string slotName, object value);
}

/// <summary>
/// 状态槽位集合
/// 通用的状态存储，按字符串键索引
/// </summary>
public sealed class StateSlots
{
    private readonly Dictionary<string, SlotValue> _slots = new Dictionary<string, SlotValue>();
    private readonly IStateSlotValueCloner _valueCloner;
    private long _version;

    /// <summary>
    /// Creates slots with an optional strategy for mutable reference values.
    /// </summary>
    public StateSlots(IStateSlotValueCloner valueCloner = null)
    {
        _valueCloner = valueCloner;
    }

    public long Version => _version;

    public IReadOnlyList<string> Keys => new List<string>(_slots.Keys);

    public bool Has(string slotName) => _slots.ContainsKey(slotName);

    public bool TryGetValue(string slotName, out object value)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            value = slot.Value;
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGet<T>(string slotName, out T value) where T : class
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            value = slot.As<T>();
            return value != null;
        }
        value = default(T);
        return false;
    }

    public T Get<T>(string slotName) where T : class
    {
        if (_slots.TryGetValue(slotName, out var slot))
            return slot.As<T>();
        return default(T);
    }

    public float GetFloat(string slotName, float defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is float f) return f;
            if (slot.Value is int i) return i;
        }
        return defaultValue;
    }

    public float GetFloat(string slotName)
    {
        return GetFloat(slotName, 0f);
    }

    public int GetInt(string slotName, int defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is int i) return i;
            if (slot.Value is float f) return (int)f;
        }
        return defaultValue;
    }

    public int GetInt(string slotName)
    {
        return GetInt(slotName, 0);
    }

    public bool GetBool(string slotName, bool defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is bool b) return b;
        }
        return defaultValue;
    }

    public bool GetBool(string slotName)
    {
        return GetBool(slotName, false);
    }

    /// <summary>
    /// 获取 Vector3 类型
    /// </summary>
    public Vector3 GetPosition(string slotName)
    {
        if (_slots.TryGetValue(slotName, out var slot) && slot.Value is Vector3 v)
            return v;
        return Vector3.Zero;
    }

    /// <summary>
    /// 获取 Quaternion 类型
    /// </summary>
    public Quaternion GetQuaternion(string slotName)
    {
        if (_slots.TryGetValue(slotName, out var slot) && slot.Value is Quaternion q)
            return q;
        return Quaternion.Identity;
    }

    public void Set(string slotName, SlotValue value)
    {
        _slots[slotName] = value;
        _version++;
    }

    public void Set(string slotName, object value)
    {
        _slots[slotName] = new SlotValue(value);
        _version++;
    }

    public void Remove(string slotName)
    {
        if (_slots.Remove(slotName))
            _version++;
    }

    /// <summary>Removes every slot from the current state.</summary>
    public void Clear()
    {
        if (_slots.Count == 0) return;
        _slots.Clear();
        _version++;
    }

    /// <summary>
    /// 复制槽位
    /// </summary>
    public StateSlots Clone()
    {
        var clone = new StateSlots(_valueCloner);
        foreach (var kvp in _slots)
        {
            clone._slots[kvp.Key] = CloneSlotValue(kvp.Key, kvp.Value);
        }
        clone._version = _version;
        return clone;
    }

    /// <summary>
    /// 从另一个 StateSlots 覆盖
    /// </summary>
    public void OverwriteFrom(StateSlots other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (ReferenceEquals(this, other)) return;

        // Clone before commit so an unsupported reference value cannot leave a partial state.
        var replacement = new Dictionary<string, SlotValue>(other._slots.Count);
        foreach (var kvp in other._slots)
        {
            replacement[kvp.Key] = CloneSlotValue(kvp.Key, kvp.Value);
        }

        _slots.Clear();
        foreach (var kvp in replacement)
        {
            _slots[kvp.Key] = kvp.Value;
        }
        _version++;
    }

    private SlotValue CloneSlotValue(string slotName, SlotValue slot)
    {
        var value = slot.Value;
        if (value == null || value is string || value.GetType().IsValueType)
            return slot;

        if (_valueCloner == null)
        {
            throw new InvalidOperationException(
                $"State slot '{slotName}' contains mutable reference type '{value.GetType().FullName}'. " +
                $"Construct StateSlots with an {nameof(IStateSlotValueCloner)} to snapshot it safely.");
        }

        var clonedValue = _valueCloner.Clone(slotName, value);
        if (clonedValue == null || !value.GetType().IsInstanceOfType(clonedValue))
        {
            throw new InvalidOperationException(
                $"The clone strategy for state slot '{slotName}' must return a non-null " +
                $"'{value.GetType().FullName}' value.");
        }

        return new SlotValue(clonedValue);
    }
}

/// <summary>
/// 预测监听器
/// </summary>
public interface IPredictionListener
{
    void OnPredictionApplied(Frame frame, StateSlots state);
    void OnServerStateApplied(Frame frame, StateSlots state);
    void OnRollbackStarted(Frame frame, ConflictLevel level);
}

/// <summary>
/// 快照存储
/// </summary>
public interface ISnapshotStore
{
    /// <summary>Captures state independently from subsequent source mutations.</summary>
    void Record(Frame frame, StateSlots state);

    /// <summary>Returns an independent snapshot that callers may mutate safely.</summary>
    StateSlots Get(Frame frame);
    void PruneBefore(Frame frame);
    void Clear();
}

/// <summary>
/// 默认使用稀疏帧索引的快照存储，也可注入环形帧后端。
/// </summary>
public sealed class DictionarySnapshotStore : ISnapshotStore, IBufferCapacityControl
{
    private readonly IFrameIndexedBuffer<StateSlots> _snapshots;

    public DictionarySnapshotStore(int maxFrames)
        : this(new SparseFrameIndexedBuffer<StateSlots>(maxFrames))
    {
    }

    /// <summary>Creates snapshot history over an explicitly selected frame storage backend.</summary>
    public DictionarySnapshotStore(IFrameIndexedBuffer<StateSlots> storage)
    {
        _snapshots = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public int Capacity => _snapshots.Capacity;

    public void Record(Frame frame, StateSlots state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        _snapshots.Store(frame.Value, state.Clone());
    }

    public bool TrySetCapacity(int capacity)
    {
        return _snapshots.TrySetCapacity(capacity);
    }

    public StateSlots Get(Frame frame)
    {
        StateSlots result;
        return _snapshots.TryGet(frame.Value, out result) ? result.Clone() : null;
    }

    public void PruneBefore(Frame frame)
    {
        _snapshots.RemoveBefore(frame.Value);
    }

    public void Clear()
    {
        _snapshots.Clear();
    }
}

/// <summary>
/// 输入历史
/// </summary>
public interface IInputHistory
{
    /// <summary>Retains one command under its original prediction frame.</summary>
    void Record(Frame frame, IInputCommand input);

    /// <summary>Returns immutable batches for every frame in the requested replay interval.</summary>
    IReadOnlyList<InputFrameBatch> GetFrameBatches(Frame from, Frame to);

    void Clear();
}

/// <summary>
/// In-memory input history bounded by prediction frames with an injectable storage backend.
/// </summary>
public sealed class InputHistory : IInputHistory, IBufferCapacityControl
{
    private readonly IFrameIndexedBuffer<List<IInputCommand>> _inputs;

    public InputHistory(int maxFrames)
        : this(new SparseFrameIndexedBuffer<List<IInputCommand>>(maxFrames))
    {
    }

    /// <summary>Creates input history over an explicitly selected frame storage backend.</summary>
    public InputHistory(IFrameIndexedBuffer<List<IInputCommand>> storage)
    {
        _inputs = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public int Capacity => _inputs.Capacity;

    public void Record(Frame frame, IInputCommand input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!_inputs.TryGet(frame.Value, out var inputs))
        {
            inputs = new List<IInputCommand>();
            _inputs.Store(frame.Value, inputs);
        }
        inputs.Add(input);
    }

    public bool TrySetCapacity(int capacity)
    {
        return _inputs.TrySetCapacity(capacity);
    }

    /// <summary>
    /// Captures every frame in the requested interval, including frames without commands.
    /// </summary>
    public IReadOnlyList<InputFrameBatch> GetFrameBatches(Frame from, Frame to)
    {
        var result = new List<InputFrameBatch>();
        var frame = new Frame(from.Value + 1);
        while (frame <= to)
        {
            IInputCommand[] snapshot;
            if (_inputs.TryGet(frame.Value, out var inputs))
                snapshot = inputs.ToArray();
            else
                snapshot = Array.Empty<IInputCommand>();

            result.Add(new InputFrameBatch(frame, snapshot));
            frame = new Frame(frame.Value + 1);
        }
        return result;
    }

    public void Clear() => _inputs.Clear();
}

/// <summary>
/// Immutable view of the commands recorded for one prediction frame.
/// Command instances must remain immutable while retained in history.
/// </summary>
public sealed class InputFrameBatch
{
    public InputFrameBatch(Frame frame, IReadOnlyList<IInputCommand> inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));

        var snapshot = new IInputCommand[inputs.Count];
        for (var i = 0; i < inputs.Count; i++)
        {
            snapshot[i] = inputs[i] ?? throw new ArgumentException(
                "Input frame batches cannot contain null commands.",
                nameof(inputs));
        }

        Frame = frame;
        Inputs = Array.AsReadOnly(snapshot);
    }

    /// <summary>The original prediction frame.</summary>
    public Frame Frame { get; }

    /// <summary>Commands in their original within-frame order.</summary>
    public IReadOnlyList<IInputCommand> Inputs { get; }
}

}

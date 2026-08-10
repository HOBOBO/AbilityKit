using System.Collections.Generic;

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
/// 状态槽位集合
/// 通用的状态存储，按字符串键索引
/// </summary>
public sealed class StateSlots
{
    private readonly Dictionary<string, SlotValue> _slots = new Dictionary<string, SlotValue>();
    private long _version;

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

    /// <summary>
    /// 复制槽位
    /// </summary>
    public StateSlots Clone()
    {
        var clone = new StateSlots();
        foreach (var kvp in _slots)
        {
            clone._slots[kvp.Key] = kvp.Value;
        }
        clone._version = _version;
        return clone;
    }

    /// <summary>
    /// 从另一个 StateSlots 覆盖
    /// </summary>
    public void OverwriteFrom(StateSlots other)
    {
        if (other == null) throw new System.ArgumentNullException(nameof(other));
        if (ReferenceEquals(this, other)) return;

        _slots.Clear();
        foreach (var kvp in other._slots)
        {
            _slots[kvp.Key] = kvp.Value;
        }
        _version++;
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
    void Record(Frame frame, StateSlots state);
    StateSlots Get(Frame frame);
    void PruneBefore(Frame frame);
}

/// <summary>
/// 基于字典的快照存储
/// </summary>
public sealed class DictionarySnapshotStore : ISnapshotStore
{
    private readonly Dictionary<Frame, StateSlots> _snapshots = new Dictionary<Frame, StateSlots>();
    private readonly int _maxFrames;

    public DictionarySnapshotStore(int maxFrames)
    {
        _maxFrames = maxFrames;
    }

    public void Record(Frame frame, StateSlots state)
    {
        _snapshots[frame] = state.Clone();
        if (_snapshots.Count > _maxFrames)
        {
            Frame oldest = Frame.Zero;
            bool hasOldest = false;
            foreach (var k in _snapshots.Keys)
            {
                if (!hasOldest || k < oldest)
                {
                    oldest = k;
                    hasOldest = true;
                }
            }
            if (hasOldest)
                _snapshots.Remove(oldest);
        }
    }

    public StateSlots Get(Frame frame)
    {
        StateSlots result;
        return _snapshots.TryGetValue(frame, out result) ? result : null;
    }

    public void PruneBefore(Frame frame)
    {
        var keys = new List<Frame>();
        foreach (var k in _snapshots.Keys)
            if (k < frame) keys.Add(k);
        foreach (var k in keys)
            _snapshots.Remove(k);
    }
}

/// <summary>
/// 输入历史
/// </summary>
public sealed class InputHistory
{
    private readonly Dictionary<Frame, List<IInputCommand>> _inputs = new Dictionary<Frame, List<IInputCommand>>();
    private readonly int _maxFrames;

    public InputHistory(int maxFrames)
    {
        _maxFrames = maxFrames;
    }

    public void Record(Frame frame, IInputCommand input)
    {
        if (!_inputs.ContainsKey(frame))
            _inputs[frame] = new List<IInputCommand>();
        _inputs[frame].Add(input);

        if (_inputs.Count > _maxFrames)
        {
            Frame oldest = Frame.Zero;
            bool hasOldest = false;
            foreach (var k in _inputs.Keys)
            {
                if (!hasOldest || k < oldest)
                {
                    oldest = k;
                    hasOldest = true;
                }
            }
            if (hasOldest)
                _inputs.Remove(oldest);
        }
    }

    public List<IInputCommand> GetInputs(Frame from, Frame to)
    {
        var result = new List<IInputCommand>();
        var f = new Frame(from.Value + 1);
        var end = new Frame(to.Value);
        while (f <= end)
        {
            if (_inputs.TryGetValue(f, out var inputs))
                result.AddRange(inputs);
            f = new Frame(f.Value + 1);
        }
        return result;
    }

    public void Clear() => _inputs.Clear();
}

}

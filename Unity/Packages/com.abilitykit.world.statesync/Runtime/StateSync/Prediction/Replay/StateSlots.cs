using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Prediction
{

/// <summary>
/// 预测处理器读写协调器共享槽位；宿主保证调用顺序与各处理器的状态所有权。
/// </summary>
public interface IPredictionHandler
{
    /// <summary>
    /// 处理器的业务名称，不用于自动排序或去重。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 声明是否参与预测、校验和权威应用。
    /// </summary>
    PredictionStrategy Strategy { get; }

    /// <summary>
    /// 声明所需槽位模式；协调器不会自动按此列表检查或填充槽位。
    /// </summary>
    IReadOnlyList<string> RequiredSlots { get; }

    /// <summary>
    /// 在指定帧将命令应用到活槽位；同一命令可能在重演中再次执行。
    /// </summary>
    void Predict(IInputCommand input, StateSlots slots, Frame frame);

    /// <summary>
    /// 比较预测槽位与权威槽位并报告冲突；不应修改任一输入。
    /// </summary>
    PredictionResult Validate(StateSlots predicted, StateSlots server);

    /// <summary>
    /// 冲突时在协调器覆盖槽位后应用业务权威规则；不应修改源权威槽位。
    /// </summary>
    void ApplyServerState(StateSlots server, StateSlots current);
}

/// <summary>
/// 为槽位快照提供自定义引用值复制；空值、值类型与字符串沿用默认策略。
/// </summary>
public interface IStateSlotValueCloner
{
    /// <summary>
    /// 返回非空且可赋值给原运行时类型的独立副本；实现负责隔离嵌套可变对象。
    /// </summary>
    object Clone(string slotName, object value);
}

/// <summary>
/// 按字符串名称索引的局部预测状态，不提供并发保护；读取和写入均可能直接持有引用。
/// </summary>
public sealed class StateSlots
{
    private readonly Dictionary<string, SlotValue> _slots = new Dictionary<string, SlotValue>();
    private readonly IStateSlotValueCloner _valueCloner;
    private long _version;

    /// <summary>
    /// 创建空槽位集合；没有复制策略时，含非字符串引用的快照复制会失败。
    /// </summary>
    public StateSlots(IStateSlotValueCloner valueCloner = null)
    {
        _valueCloner = valueCloner;
    }

    /// <summary>
    /// 返回槽位写入或实际移除等操作的版本；引用内部变化不会自动计入。
    /// </summary>
    public long Version => _version;

    /// <summary>
    /// 返回与集合结构独立的键列表，不承诺排序。
    /// </summary>
    public IReadOnlyList<string> Keys => new List<string>(_slots.Keys);

    /// <summary>
    /// 检查名称是否存在；值为空的已登记槽位也算存在。
    /// </summary>
    public bool Has(string slotName) => _slots.ContainsKey(slotName);

    /// <summary>
    /// 按名称读取原始值；找到空值时也返回成功，未命中时输出空值。
    /// </summary>
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

    /// <summary>
    /// 读取可赋值给目标引用类型的非空值，类型不匹配时返回失败。
    /// </summary>
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

    /// <summary>
    /// 返回可赋值给目标引用类型的原始值；找不到或不匹配时为空。
    /// </summary>
    public T Get<T>(string slotName) where T : class
    {
        if (_slots.TryGetValue(slotName, out var slot))
            return slot.As<T>();
        return default(T);
    }

    /// <summary>
    /// 读取单精度浮点或转换整数；其它类型或缺失时返回指定默认值。
    /// </summary>
    public float GetFloat(string slotName, float defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is float f) return f;
            if (slot.Value is int i) return i;
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取数值槽位，缺失或不匹配时返回零。
    /// </summary>
    public float GetFloat(string slotName)
    {
        return GetFloat(slotName, 0f);
    }

    /// <summary>
    /// 读取整数或将单精度浮点按显式整数转换规则截断；其它类型或缺失时返回指定默认值。
    /// </summary>
    public int GetInt(string slotName, int defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is int i) return i;
            if (slot.Value is float f) return (int)f;
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取整数或截断单精度浮点，缺失或不匹配时返回零。
    /// </summary>
    public int GetInt(string slotName)
    {
        return GetInt(slotName, 0);
    }

    /// <summary>
    /// 读取布尔值，不将数字或字符串转换为布尔；缺失或不匹配时返回指定默认值。
    /// </summary>
    public bool GetBool(string slotName, bool defaultValue)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            if (slot.Value is bool b) return b;
        }
        return defaultValue;
    }

    /// <summary>
    /// 读取布尔值，缺失或不匹配时返回假。
    /// </summary>
    public bool GetBool(string slotName)
    {
        return GetBool(slotName, false);
    }

    /// <summary>
    /// 读取预测向量值；缺失或不匹配时返回零向量。
    /// </summary>
    public Vector3 GetPosition(string slotName)
    {
        if (_slots.TryGetValue(slotName, out var slot) && slot.Value is Vector3 v)
            return v;
        return Vector3.Zero;
    }

    /// <summary>
    /// 读取预测四元数值；缺失或不匹配时返回单位四元数。
    /// </summary>
    public Quaternion GetQuaternion(string slotName)
    {
        if (_slots.TryGetValue(slotName, out var slot) && slot.Value is Quaternion q)
            return q;
        return Quaternion.Identity;
    }

    /// <summary>
    /// 存入槽位包装值并增加版本，可能替换已有值；不做快照复制。
    /// </summary>
    public void Set(string slotName, SlotValue value)
    {
        _slots[slotName] = value;
        _version++;
    }

    /// <summary>
    /// 包装并存入给定值，增加版本；引用值保持原引用。
    /// </summary>
    public void Set(string slotName, object value)
    {
        _slots[slotName] = new SlotValue(value);
        _version++;
    }

    /// <summary>
    /// 删除指定名称；仅在命中时增加版本。
    /// </summary>
    public void Remove(string slotName)
    {
        if (_slots.Remove(slotName))
            _version++;
    }

    /// <summary>
    /// 清空槽位；仅在原集合非空时增加版本。
    /// </summary>
    public void Clear()
    {
        if (_slots.Count == 0) return;
        _slots.Clear();
        _version++;
    }

    /// <summary>
    /// 复制槽位和版本，沿用当前复制策略；含非字符串引用时依赖该策略，不支持的值会抛出异常。
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
    /// 使用接收方的复制策略构造替换值，复制成功后才覆盖槽位并增加版本；传入自身时无操作。
    /// </summary>
    public void OverwriteFrom(StateSlots other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (ReferenceEquals(this, other)) return;

        /*
         * 先复制全部替换值，再清理旧集合，避免复制策略拒绝时留下部分覆盖。
         */
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
/// 在驱动执行线上同步观察预测与校正；协调器隔离单项监听器异常，不支持在回调中重入或修改订阅集合。
/// </summary>
public interface IPredictionListener
{
    /// <summary>
    /// 普通预测帧完成后收到当前帧和活槽位；不是快照，也不包含逐帧重演通知。
    /// </summary>
    void OnPredictionApplied(Frame frame, StateSlots state);
    /// <summary>
    /// 收到已处理的权威帧和最终活槽位，后者可能已包含输入重演结果。
    /// </summary>
    void OnServerStateApplied(Frame frame, StateSlots state);
    /// <summary>
    /// 在重演前收到权威校正起点和冲突等级。
    /// </summary>
    void OnRollbackStarted(Frame frame, ConflictLevel level);
}

/// <summary>
/// 按预测帧隔离保存槽位副本的历史，不负责权威准入。
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// 存入与源槽位后续修改隔离的帧快照。
    /// </summary>
    void Record(Frame frame, StateSlots state);

    /// <summary>
    /// 返回独立快照供调用方使用；没有该帧时返回空值。
    /// </summary>
    StateSlots Get(Frame frame);
    /// <summary>
    /// 移除严格早于给定帧的历史。
    /// </summary>
    void PruneBefore(Frame frame);
    /// <summary>
    /// 清空全部预测快照。
    /// </summary>
    void Clear();
}

/// <summary>
/// 采用独立的帧索引后端，默认使用稀疏存储；协调器应在单一执行线上使用它。
/// </summary>
public sealed class DictionarySnapshotStore : ISnapshotStore, IBufferCapacityControl
{
    private readonly IFrameIndexedBuffer<StateSlots> _snapshots;

    /// <summary>
    /// 以给定帧容量创建稀疏存储。
    /// </summary>
    public DictionarySnapshotStore(int maxFrames)
        : this(new SparseFrameIndexedBuffer<StateSlots>(maxFrames))
    {
    }

    /// <summary>
    /// 接管给定后端的历史操作；外部不应另行写入或并发访问同一后端。
    /// </summary>
    public DictionarySnapshotStore(IFrameIndexedBuffer<StateSlots> storage)
    {
        _snapshots = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// 返回后端当前的帧容量。
    /// </summary>
    public int Capacity => _snapshots.Capacity;

    /// <summary>
    /// 克隆非空槽位并存入指定帧；复制失败不替换旧记录。
    /// </summary>
    public void Record(Frame frame, StateSlots state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        _snapshots.Store(frame.Value, state.Clone());
    }

    /// <summary>
    /// 尝试改变后端帧容量，拒绝与裁剪规则由后端决定。
    /// </summary>
    public bool TrySetCapacity(int capacity)
    {
        return _snapshots.TrySetCapacity(capacity);
    }

    /// <summary>
    /// 命中时克隆保存的槽位，未命中返回空值。
    /// </summary>
    public StateSlots Get(Frame frame)
    {
        StateSlots result;
        return _snapshots.TryGet(frame.Value, out result) ? result.Clone() : null;
    }

    /// <summary>
    /// 移除帧号严格早于给定帧的快照。
    /// </summary>
    public void PruneBefore(Frame frame)
    {
        _snapshots.RemoveBefore(frame.Value);
    }

    /// <summary>
    /// 清空该后端的预测快照，不改变容量。
    /// </summary>
    public void Clear()
    {
        _snapshots.Clear();
    }
}

/// <summary>
/// 按原始预测帧保留命令与同帧顺序；命令引用本身须保持不可变。
/// </summary>
public interface IInputHistory
{
    /// <summary>
    /// 将命令保留在原预测帧的同帧批次中。
    /// </summary>
    void Record(Frame frame, IInputCommand input);

    /// <summary>
    /// 按升序返回严格晚于起点且不晚于终点的预测帧批次；批次内命令保持原顺序。
    /// </summary>
    IReadOnlyList<InputFrameBatch> GetFrameBatches(Frame from, Frame to);

    /// <summary>
    /// 清空全部输入历史。
    /// </summary>
    void Clear();
}

/// <summary>
/// 按帧有界地记录输入批次，支持注入帧存储；应在单一执行线上使用。
/// </summary>
public sealed class InputHistory : IInputHistory, IBufferCapacityControl
{
    private readonly IFrameIndexedBuffer<List<IInputCommand>> _inputs;

    /// <summary>
    /// 以给定帧容量创建稀疏输入历史。
    /// </summary>
    public InputHistory(int maxFrames)
        : this(new SparseFrameIndexedBuffer<List<IInputCommand>>(maxFrames))
    {
    }

    /// <summary>
    /// 接管给定后端的历史操作；不要从外部修改保留的批次列表。
    /// </summary>
    public InputHistory(IFrameIndexedBuffer<List<IInputCommand>> storage)
    {
        _inputs = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// 返回后端当前的帧容量，不是命令数。
    /// </summary>
    public int Capacity => _inputs.Capacity;

    /// <summary>
    /// 将非空命令引用追加到指定帧，保持同帧记录次序。
    /// </summary>
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

    /// <summary>
    /// 尝试改变输入历史容量，后端决定淘汰哪些帧。
    /// </summary>
    public bool TrySetCapacity(int capacity)
    {
        return _inputs.TrySetCapacity(capacity);
    }

    /// <summary>
    /// 按帧升序返回左开右闭区间的批次；无命令或已淘汰的帧以空批次表示，不能据此判断历史是否完整。
    /// 区间长度由调用方限制，批次复制容器而不复制命令。
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

    /// <summary>
    /// 清空输入批次，不改变帧容量。
    /// </summary>
    public void Clear() => _inputs.Clear();
}

/// <summary>
/// 一帧命令的只读集合，拥有独立的集合结构但保留原命令引用。
/// </summary>
public sealed class InputFrameBatch
{
    /// <summary>
    /// 保存帧号并复制命令列表结构；拒绝空列表引用或空命令，允许零条命令。
    /// </summary>
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

    /// <summary>
    /// 返回这些命令最初记录的预测帧。
    /// </summary>
    public Frame Frame { get; }

    /// <summary>
    /// 按原同帧顺序返回只读列表；命令实例不会复制。
    /// </summary>
    public IReadOnlyList<IInputCommand> Inputs { get; }
}

}

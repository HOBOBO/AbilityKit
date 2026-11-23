using System;

namespace AbilityKit.Ability.StateSync
{

/// <summary>
/// 由业务定义的预测命令标记；命令不是权威输入终结凭据，进入历史后须保持不可变。
/// </summary>
public interface IInputCommand
{
}

/// <summary>
/// 局部预测使用的整数帧号，既不是时间戳也不是帧同步权威进度。
/// </summary>
public readonly struct Frame : IEquatable<Frame>, IComparable<Frame>
{
    /// <summary>
    /// 原始整数帧号。
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// 直接保存帧号，不做范围校验。
    /// </summary>
    public Frame(int value) => Value = value;

    /// <summary>
    /// 按整数偏移推进帧号，不限制有效范围。
    /// </summary>
    public static Frame operator +(Frame a, int b) => new Frame(a.Value + b);
    /// <summary>
    /// 按整数偏移回退帧号，不限制有效范围。
    /// </summary>
    public static Frame operator -(Frame a, int b) => new Frame(a.Value - b);
    /// <summary>
    /// 比较原始帧号是否相等。
    /// </summary>
    public static bool operator ==(Frame a, Frame b) => a.Value == b.Value;
    /// <summary>
    /// 比较原始帧号是否不等。
    /// </summary>
    public static bool operator !=(Frame a, Frame b) => a.Value != b.Value;
    /// <summary>
    /// 判断左侧帧号是否更早。
    /// </summary>
    public static bool operator <(Frame a, Frame b) => a.Value < b.Value;
    /// <summary>
    /// 判断左侧帧号是否更晚。
    /// </summary>
    public static bool operator >(Frame a, Frame b) => a.Value > b.Value;
    /// <summary>
    /// 判断左侧帧号是否不晚于右侧。
    /// </summary>
    public static bool operator <=(Frame a, Frame b) => a.Value <= b.Value;
    /// <summary>
    /// 判断左侧帧号是否不早于右侧。
    /// </summary>
    public static bool operator >=(Frame a, Frame b) => a.Value >= b.Value;

    /// <summary>
    /// 按原始帧号判定相等。
    /// </summary>
    public bool Equals(Frame other) => Value == other.Value;
    /// <summary>
    /// 对象为相同帧号的帧值时才相等。
    /// </summary>
    public override bool Equals(object obj) => obj is Frame other && Equals(other);
    /// <summary>
    /// 返回用于集合索引的帧号哈希，不是世界状态哈希。
    /// </summary>
    public override int GetHashCode() => Value.GetHashCode();
    /// <summary>
    /// 按整数帧号排序。
    /// </summary>
    public int CompareTo(Frame other) => Value.CompareTo(other.Value);
    /// <summary>
    /// 返回带 F 前缀的诊断文本，不用于持久化。
    /// </summary>
    public override string ToString() => $"F{Value}";

    /// <summary>
    /// 表示尚未取得有效权威状态帧的负一哨兵。
    /// </summary>
    public static Frame Invalid => new Frame(-1);
    /// <summary>
    /// 表示局部预测的零帧。
    /// </summary>
    public static Frame Zero => new Frame(0);
}

/// <summary>
/// 处理器声明的参与策略；协调器只区分禁用与启用，重演由协调器历史选项决定。
/// </summary>
public enum PredictionStrategy
{
    /// <summary>
    /// 不执行此处理器的预测、校验或权威应用。
    /// </summary>
    None,
    /// <summary>
    /// 参与预测、校验和权威应用。
    /// </summary>
    Optimistic,
    /// <summary>
    /// 参与预测、校验和权威应用；不单独开启历史或重演。
    /// </summary>
    OptimisticWithRollback
}

/// <summary>
/// 处理器报告的冲突等级；协调器使用失败结果中的最高等级。
/// </summary>
public enum ConflictLevel
{
    /// <summary>
    /// 没有需要校正的冲突。
    /// </summary>
    None,
    /// <summary>
    /// 业务判定为轻微冲突。
    /// </summary>
    Minor,
    /// <summary>
    /// 业务判定为主要冲突。
    /// </summary>
    Major,
    /// <summary>
    /// 业务判定为严重冲突。
    /// </summary>
    Critical
}

/// <summary>
/// 单个处理器的校验结果；协调器不解释附带文本。
/// </summary>
public readonly struct PredictionResult
{
    /// <summary>
    /// 表示本项校验是否通过。
    /// </summary>
    public bool Success { get; }
    /// <summary>
    /// 失败时报告给协调器的冲突等级。
    /// </summary>
    public ConflictLevel Level { get; }
    /// <summary>
    /// 附带的诊断文本，不参与冲突决策。
    /// </summary>
    public string Message { get; }

    private PredictionResult(bool success, ConflictLevel level, string message)
    {
        Success = success;
        Level = level;
        Message = message;
    }

    /// <summary>
    /// 返回通过结果，不附带冲突等级或消息。
    /// </summary>
    public static PredictionResult Ok() => new PredictionResult(true, ConflictLevel.None, string.Empty);
    /// <summary>
    /// 返回轻微冲突及调用方消息。
    /// </summary>
    public static PredictionResult Minor(string msg) => new PredictionResult(false, ConflictLevel.Minor, msg);
    /// <summary>
    /// 返回主要冲突及调用方消息。
    /// </summary>
    public static PredictionResult Major(string msg) => new PredictionResult(false, ConflictLevel.Major, msg);
    /// <summary>
    /// 返回严重冲突及调用方消息。
    /// </summary>
    public static PredictionResult Critical(string msg) => new PredictionResult(false, ConflictLevel.Critical, msg);
}

/// <summary>
/// 携带槽位值和类型信息；包装和读取不复制引用。
/// </summary>
public readonly struct SlotValue
{
    /// <summary>
    /// 返回原始值或引用，可能为空。
    /// </summary>
    public object Value { get; }
    /// <summary>
    /// 返回构造时记录的运行时类型；空值构造时为对象类型，默认结构值则为空。
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// 包装给定值并记录类型，不复制引用或检查是否可快照化。
    /// </summary>
    public SlotValue(object value)
    {
        Value = value;
        ValueType = value != null ? value.GetType() : typeof(object);
    }

    /// <summary>
    /// 读取可赋值给目标引用类型的值；不匹配时返回空值，不做转换。
    /// </summary>
    public T As<T>() where T : class => Value is T t ? t : default;

    /// <summary>
    /// 包装整数槽位值。
    /// </summary>
    public static implicit operator SlotValue(int v) => new SlotValue(v);
    /// <summary>
    /// 包装单精度槽位值。
    /// </summary>
    public static implicit operator SlotValue(float v) => new SlotValue(v);
    /// <summary>
    /// 包装布尔槽位值。
    /// </summary>
    public static implicit operator SlotValue(bool v) => new SlotValue(v);
    /// <summary>
    /// 包装字符串槽位值。
    /// </summary>
    public static implicit operator SlotValue(string v) => new SlotValue(v);
}

/// <summary>
/// 局部预测槽位使用的独立三维浮点值；不等同于 Core 数学值或快照模型。
/// </summary>
public readonly struct Vector3
{
    /// <summary>
    /// 业务坐标系的横坐标分量。
    /// </summary>
    public float X { get; }
    /// <summary>
    /// 业务坐标系的纵坐标分量。
    /// </summary>
    public float Y { get; }
    /// <summary>
    /// 业务坐标系的深度坐标分量。
    /// </summary>
    public float Z { get; }

    /// <summary>
    /// 保留三个原始分量，不换算坐标或单位。
    /// </summary>
    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 返回所有分量均为零的向量。
    /// </summary>
    public static Vector3 Zero => new Vector3(0, 0, 0);
    /// <summary>
    /// 返回三维欧氏长度，单位与输入坐标相同。
    /// </summary>
    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
    /// <summary>
    /// 返回向 XZ 平面的投影长度。
    /// </summary>
    public float LengthXZ => MathF.Sqrt(X * X + Z * Z);

    /// <summary>
    /// 按分量相加，不转换坐标系。
    /// </summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    /// <summary>
    /// 按分量计算左向量减右向量。
    /// </summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    /// <summary>
    /// 用同一标量缩放各分量。
    /// </summary>
    public static Vector3 operator *(Vector3 a, float scalar) => new Vector3(a.X * scalar, a.Y * scalar, a.Z * scalar);
}

/// <summary>
/// 局部预测槽位使用的独立四元数值，不等同于 Core 或快照模型。
/// </summary>
public readonly struct Quaternion
{
    /// <summary>
    /// 四元数向量部的第一分量。
    /// </summary>
    public float X { get; }
    /// <summary>
    /// 四元数向量部的第二分量。
    /// </summary>
    public float Y { get; }
    /// <summary>
    /// 四元数向量部的第三分量。
    /// </summary>
    public float Z { get; }
    /// <summary>
    /// 四元数标量部。
    /// </summary>
    public float W { get; }

    /// <summary>
    /// 按参数顺序保存四个分量，不归一化或验证旋转有效性。
    /// </summary>
    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>
    /// 返回表示无旋转的单位四元数。
    /// </summary>
    public static Quaternion Identity => new Quaternion(0, 0, 0, 1);
}

/// <summary>
/// 声明类使用的槽位元数据；协调器不会据此自动验证或安排处理器顺序。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class NeedsSlotAttribute : Attribute
{
    /// <summary>
    /// 声明所需的槽位名称。
    /// </summary>
    public string SlotName { get; }
    /// <summary>
    /// 由外部使用方解释的必需标志。
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// 保存槽位名称和必需标志，不检查当前状态。
    /// </summary>
    public NeedsSlotAttribute(string slotName, bool required)
    {
        SlotName = slotName;
        Required = required;
    }
}

/// <summary>
/// 声明类提供的槽位元数据；属性本身不写入槽位。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProvidesSlotAttribute : Attribute
{
    /// <summary>
    /// 声明可提供的槽位名称。
    /// </summary>
    public string SlotName { get; }
    /// <summary>
    /// 记录用途说明，不参与匹配规则。
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 保存外部使用方提供的名称和说明。
    /// </summary>
    public ProvidesSlotAttribute(string slotName, string description)
    {
        SlotName = slotName;
        Description = description;
    }
}

/// <summary>
/// 按名称或末尾点星号前缀筛选槽位；不实现通用通配表达式。
/// </summary>
public static class SlotPattern
{
    /// <summary>
    /// 先比较完整名称；末尾点星号按包含最后一个点的前缀匹配，沿用字符串默认前缀比较规则。
    /// </summary>
    public static bool Matches(string pattern, string slotName)
    {
        if (pattern == slotName) return true;
        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return slotName.StartsWith(prefix);
        }
        return false;
    }

    /// <summary>
    /// 按源枚举顺序惰性返回匹配项，不排序或去重。
    /// </summary>
    public static System.Collections.Generic.IEnumerable<string> GetMatchingSlots(string pattern, System.Collections.Generic.IEnumerable<string> allSlots)
    {
        foreach (var slot in allSlots)
        {
            if (Matches(pattern, slot))
                yield return slot;
        }
    }
}

}

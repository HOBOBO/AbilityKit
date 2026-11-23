using System;
using MemoryPack;

namespace AbilityKit.Ability.StateSync.Snapshot
{
    /// <summary>
    /// 快照兼容用的三分量单精度值类型，与核心数学类型独立，转换不改变单位或坐标系。
    /// </summary>
    [MemoryPackable]
    public partial struct Vec3
    {
        /// <summary>
        /// 按 X、Y、Z 排列的可变分量；默认值均为 0，不规定业务单位。
        /// </summary>
        public float X, Y, Z;

        /// <summary>
        /// 按给定值保存三个单精度分量，不作范围校验。
        /// </summary>
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 三个分量均为 0 的向量。
        /// </summary>
        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        /// <summary>
        /// 三个分量均为 1 的向量。
        /// </summary>
        public static readonly Vec3 One = new Vec3(1f, 1f, 1f);
        /// <summary>
        /// 仅 Y 分量为 1 的向量。
        /// </summary>
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        /// <summary>
        /// 返回按单精度分量计算的长度平方，不取平方根。
        /// </summary>
        public float MagnitudeSquared() => X * X + Y * Y + Z * Z;
        /// <summary>
        /// 返回长度平方的平方根，结果为单精度。
        /// </summary>
        public float Magnitude() => (float)Math.Sqrt(MagnitudeSquared());

        /// <summary>
        /// 逐分量求和，返回新值，不修改操作数。
        /// </summary>
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        /// <summary>
        /// 按左减右逐分量求差，返回新值。
        /// </summary>
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        /// <summary>
        /// 将每个分量乘给定标量，返回新值。
        /// </summary>
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        /// <summary>
        /// 将每个分量除以给定标量，遵循浮点除法规则，不拒绝零除数。
        /// </summary>
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        /// <summary>
        /// 要求每个分量的绝对差严格小于容差；默认容差为 0.0001，非正容差不会匹配。
        /// </summary>
        public bool ApproximatelyEquals(Vec3 other, float epsilon = 0.0001f)
        {
            return Math.Abs(X - other.X) < epsilon &&
                   Math.Abs(Y - other.Y) < epsilon &&
                   Math.Abs(Z - other.Z) < epsilon;
        }

        /// <summary>
        /// 按分量复制核心向量，保留坐标值和单位。
        /// </summary>
        public static Vec3 FromCoreVec3(AbilityKit.Core.Mathematics.Vec3 v) => new Vec3(v.X, v.Y, v.Z);

        /// <summary>
        /// 按分量转换为核心向量，不额外缩放。
        /// </summary>
        public AbilityKit.Core.Mathematics.Vec3 ToCoreVec3() => new AbilityKit.Core.Mathematics.Vec3(X, Y, Z);
    }

    /// <summary>
    /// 快照兼容用的四分量单精度值类型；不强制归一化，也不定义旋转等价比较。
    /// </summary>
    [MemoryPackable]
    public partial struct Quat
    {
        /// <summary>
        /// 按 X、Y、Z、W 排列的可变分量；默认全零，不等于单位四元数。
        /// </summary>
        public float X, Y, Z, W;

        /// <summary>
        /// 直接保存四个分量，不归一化或校验。
        /// </summary>
        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// 前三个分量为 0、W 为 1 的单位四元数。
        /// </summary>
        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        /// <summary>
        /// 要求四个分量的绝对差均严格小于容差，默认 0.0001。
        /// 这是分量比较，不把符号相反但旋转等价的四元数视为相等。
        /// </summary>
        public bool ApproximatelyEquals(Quat other, float epsilon = 0.0001f)
        {
            return Math.Abs(X - other.X) < epsilon &&
                   Math.Abs(Y - other.Y) < epsilon &&
                   Math.Abs(Z - other.Z) < epsilon &&
                   Math.Abs(W - other.W) < epsilon;
        }

        /// <summary>
        /// 按分量复制核心四元数，不额外归一化。
        /// </summary>
        public static Quat FromCoreQuat(AbilityKit.Core.Mathematics.Quat q) => new Quat(q.X, q.Y, q.Z, q.W);

        /// <summary>
        /// 按分量转换为核心四元数，不额外归一化。
        /// </summary>
        public AbilityKit.Core.Mathematics.Quat ToCoreQuat() => new AbilityKit.Core.Mathematics.Quat(X, Y, Z, W);
    }

    /// <summary>
    /// 保存框架级快照元数据，不包含逐实体恢复记录，也不是完整世界导出。
    /// 业务状态应组合独立类型；此处的分部声明供同一程序集编译和生成器使用，不是外部包扩展点。
    /// </summary>
    [MemoryPackable]
    public partial class WorldStateSnapshot
    {
        /// <summary>
        /// 调用方填写的世界标识，默认为 0；框架元数据哈希不包含该值。
        /// </summary>
        public ulong WorldId { get; set; }

        /// <summary>
        /// 框架格式版本，构造时使用当前版本；可由调用方修改，反序列化入口不额外校验。
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 调用方填入的快照帧，默认为 0；不等同于权威输入确认。
        /// </summary>
        public int Frame { get; set; }

        /// <summary>
        /// 快照时间元数据，默认为 0；默认检查点捕获写入 UTC Unix 毫秒，类型本身不校验。
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 调用方写入的世界标志，默认为 0；类型本身不解释标志含义。
        /// </summary>
        public uint WorldFlags { get; set; }

        /// <summary>
        /// 快照类型标记，默认为真；不表示包含完整业务世界或可独立恢复实体。
        /// </summary>
        public bool IsFullSnapshot { get; set; } = true;

        /// <summary>
        /// 框架元数据的当前版本为 1；不承诺不同版本之间自动迁移。
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 创建默认元数据对象，设置当前版本并保留完整标记默认值。
        /// </summary>
        public WorldStateSnapshot()
        {
            Version = CurrentVersion;
        }

        /// <summary>
        /// 按现有快照格式序列化所给对象，返回结果数组；格式和失败行为由 MemoryPack 处理。
        /// </summary>
        public static byte[] Serialize(WorldStateSnapshot snapshot)
        {
            return MemoryPackSerializer.Serialize(snapshot);
        }

        /// <summary>
        /// 使用现有快照格式恢复框架元数据；透传反序列化失败，不额外验证世界身份或版本。
        /// </summary>
        public static WorldStateSnapshot Deserialize(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<WorldStateSnapshot>(data);
        }

        /// <summary>
        /// 保留的反序列化别名，行为与静态反序列化入口相同。
        /// </summary>
        public static WorldStateSnapshot FromBytes(byte[] data)
        {
            return Deserialize(data);
        }

        /// <summary>
        /// 序列化当前元数据并返回结果数组，不包含单独保存的业务实体状态。
        /// </summary>
        public byte[] ToBytes()
        {
            return Serialize(this);
        }

        /// <summary>
        /// 计算框架元数据哈希，不包含业务实体状态或世界身份。
        /// </summary>
        public StateHash ComputeHash()
        {
            return StateHashComputer.Compute(this);
        }

        /// <summary>
        /// 通过现有序列化往返生成独立元数据对象；会分配缓冲，失败向调用方传播。
        /// </summary>
        public WorldStateSnapshot Clone()
        {
            var bytes = ToBytes();
            var clone = FromBytes(bytes);
            return clone;
        }
    }
}

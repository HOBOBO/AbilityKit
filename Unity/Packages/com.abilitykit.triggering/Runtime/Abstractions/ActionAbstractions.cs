using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Abstractions
{
    /// <summary>
    /// 黑板解析器（读取共享数值）
    /// 用于跨 Action 共享数值（如 Buff 全局变量）
    /// </summary>
    public interface IBlackboardResolver
    {
        /// <summary>
        /// 尝试获取黑板
        /// </summary>
        bool TryResolve(int boardId, out IBlackboard board);

        /// <summary>
        /// 获取黑板（不存在则创建）
        /// </summary>
        IBlackboard GetOrCreate(int boardId);
    }

    /// <summary>
    /// 黑板（键值对存储）
    /// </summary>
    public interface IBlackboard
    {
        /// <summary>
        /// 获取数值
        /// </summary>
        bool TryGetDouble(int keyId, out double value);

        /// <summary>
        /// 设置数值
        /// </summary>
        void Set(int keyId, double value);

        /// <summary>
        /// 获取或设置数值（索引器）
        /// </summary>
        double this[int keyId] { get; set; }

        /// <summary>
        /// 是否包含键
        /// </summary>
        bool Contains(int keyId);
    }

    /// <summary>
    /// 载荷访问器（读取单位属性、Buff等）
    /// 用于访问战斗单位的数据（生命值、位置、Buff 列表等）
    /// </summary>
    public interface IPayloadAccessor
    {
        /// <summary>
        /// 尝试获取数值载荷字段
        /// </summary>
        bool TryGetPayloadDouble(in object args, int fieldId, out double value);

        /// <summary>
        /// 尝试获取对象载荷字段
        /// </summary>
        bool TryGetPayloadObject(in object args, int fieldId, out object value);

        /// <summary>
        /// 获取目标单位（如有）
        /// </summary>
        object Target { get; }
    }

    /// <summary>
    /// 变量仓库（读写游戏全局变量）
    /// 用于访问游戏变量系统（如 "Player.Health", "Enemy.Count"）
    /// </summary>
    public interface IVariableRepository
    {
        /// <summary>
        /// 获取数值变量
        /// </summary>
        double GetNumeric(string domainId, string key);

        /// <summary>
        /// 设置数值变量
        /// </summary>
        void SetNumeric(string domainId, string key, double value);

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        bool Has(string domainId, string key);
    }

    /// <summary>
    /// 时间服务
    /// </summary>
    public interface ITimeService
    {
        /// <summary>
        /// 帧间隔时间（毫秒）
        /// </summary>
        float DeltaTimeMs { get; }

        /// <summary>
        /// 总运行时间（毫秒）
        /// </summary>
        float TotalTimeMs { get; }

        /// <summary>
        /// 当前时间戳（毫秒）
        /// </summary>
        long CurrentTimestampMs { get; }
    }

    /// <summary>
    /// 事件总线
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 发布事件（泛型）
        /// </summary>
        void Publish<TEvent>(TEvent @event);

        /// <summary>
        /// 发布事件（通过事件ID）
        /// </summary>
        void Publish(int eventId, object payload = null);

        /// <summary>
        /// 订阅事件
        /// </summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
    }

    /// <summary>
    /// 实体查找器
    /// 用于查询游戏单位（如查找最近的敌人、特定ID的单位）
    /// </summary>
    public interface IEntityFinder
    {
        /// <summary>
        /// 通过 ID 查找实体
        /// </summary>
        T FindById<T>(int entityId) where T : class;

        /// <summary>
        /// 通过标签查找实体
        /// </summary>
        IEnumerable<T> FindByTag<T>(string tag) where T : class;

        /// <summary>
        /// 查找最近的实体
        /// </summary>
        T FindNearest<T>(Vector3 position, float maxDistance = float.MaxValue) where T : class;

        /// <summary>
        /// 查找范围内的所有实体
        /// </summary>
        IEnumerable<T> FindInRange<T>(Vector3 center, float radius) where T : class;
    }

    /// <summary>
    /// 向量结构（简化版，避免依赖 Unity.Mathematics）
    /// </summary>
    public readonly struct Vector3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3 Zero => new(0, 0, 0);
        public static Vector3 One => new(1, 1, 1);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector3 Normalized => Magnitude > 0 ? this / Magnitude : Zero;

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

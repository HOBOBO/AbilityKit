namespace AbilityKit.Context
{
    /// <summary>
    /// 快照数据访问器
    /// 支持按需获取实时数据或使用快照值作为后备
    /// 
    /// 业务层实现的快照类应实现此接口
    /// </summary>
    public interface ISnapshotAccessor
    {
        /// <summary>
        /// 获取值：优先实时值，实时不可用时使用快照值
        /// </summary>
        T GetValue<T>(string key, T snapshotDefault = default);

        /// <summary>
        /// 是否可获取实时数据（上下文未销毁）
        /// </summary>
        bool IsRealtimeAvailable { get; }
    }

    /// <summary>
    /// 快照访问器静态工具类
    /// </summary>
    public static class SnapshotAccessor
    {
        /// <summary>
        /// 获取值：优先实时值，实时不可用时用快照值
        /// </summary>
        public static T Get<T>(IContextSnapshot snapshot, string key, T defaultValue = default)
        {
            if (snapshot is ISnapshotAccessor accessor)
            {
                return accessor.GetValue(key, defaultValue);
            }
            return defaultValue;
        }

        /// <summary>
        /// 检查是否可获取实时数据
        /// </summary>
        public static bool IsRealtimeAvailable(IContextSnapshot snapshot)
        {
            if (snapshot is ISnapshotAccessor accessor)
            {
                return accessor.IsRealtimeAvailable;
            }
            return false;
        }
    }
}

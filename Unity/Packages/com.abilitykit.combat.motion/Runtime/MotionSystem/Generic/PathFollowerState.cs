using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Generic
{
    /// <summary>
    /// 保存单个路径来源的完整领域数据，不包含管线、导航世界或回滚基础设施。
    /// </summary>
    /// <remarks>
    /// 结构赋值不会复制路径数组。使用来源的 CaptureState 和 RestoreState 取得独立数据边界。
    /// 默认结构不是合法重置状态；完整恢复前须验证所有字段。
    /// </remarks>
    public struct PathFollowerState
    {
        /// <summary>
        /// 按执行顺序保存的完整世界坐标路径；完整恢复要求非空引用和有限坐标。
        /// </summary>
        public Vec3[] Points;

        /// <summary>
        /// 来源创建或重新配置时冻结的每秒移动距离。
        /// </summary>
        public float Speed;

        /// <summary>
        /// 判定跳过已到达路径点的距离阈值。
        /// </summary>
        public float ArriveEpsilon;

        /// <summary>
        /// 管线仲裁使用的来源组编号，允许任意整数。
        /// </summary>
        public int GroupId;

        /// <summary>
        /// 管线仲裁使用的优先级，允许任意整数。
        /// </summary>
        public int Priority;

        /// <summary>
        /// 来源参与管线仲裁和贡献叠加的方式。
        /// </summary>
        public MotionStacking Stacking;

        /// <summary>
        /// 下一个待处理路径点的索引，允许等于路径长度。
        /// </summary>
        public int Index;

        /// <summary>
        /// 是否仍参与推进；取消时可在任意合法索引处为假。
        /// </summary>
        public bool IsActive;
    }
}

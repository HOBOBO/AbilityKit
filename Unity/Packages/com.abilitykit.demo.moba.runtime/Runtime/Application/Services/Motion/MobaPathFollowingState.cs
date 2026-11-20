using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 保存单个角色的路径规划值与来源值，不包含实体、管线或本地捕获凭证。
    /// 数组需要在捕获和恢复边界复制；该状态本身不授予结构重建资格。
    /// </summary>
    internal struct MobaPathFollowingState
    {
        /// <summary>
        /// 路径槽位所属的角色编号。
        /// </summary>
        internal int ActorId;

        /// <summary>
        /// 捕获时角色注册表中的实体版本。
        /// </summary>
        internal int EntityVersion;

        /// <summary>
        /// 是否存在规划绑定，不能从来源活动状态推断。
        /// </summary>
        internal bool BindingExists;

        /// <summary>
        /// 绑定是否仍持有一个尚未归池的来源。
        /// </summary>
        internal bool SourceExists;

        /// <summary>
        /// 来源是否仍为原管线成员，与活动状态分别保存。
        /// </summary>
        internal bool IsPipelineMember;

        /// <summary>
        /// 上一次正常重规划时观察的目标位置。
        /// </summary>
        internal Vec3 LastTarget;

        /// <summary>
        /// 上一次正常重规划以来累计的帧数，合法范围为零至二十。
        /// </summary>
        internal int FramesSinceRepath;

        /// <summary>
        /// 来源的完整路径与推进状态；无来源时必须为空。
        /// </summary>
        internal PathFollowerState? Source;
    }
}

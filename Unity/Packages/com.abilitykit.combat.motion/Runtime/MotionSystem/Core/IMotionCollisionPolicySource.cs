using AbilityKit.Combat.MotionSystem.Constraints;

namespace AbilityKit.Combat.MotionSystem.Core
{
    /// <summary>
    /// 可选接口：motion source 实现后可向 solver 声明自身的碰撞/墙体策略，覆盖 actor 默认约束。
    /// 由 <see cref="MotionPipeline"/> 选取主导贡献源（按 group 优先级）后透传至 <see cref="MotionOutput"/>。
    /// 不强制 <see cref="IMotionSource"/> 实现者实现本接口（避免破坏既有 source）。
    /// </summary>
    public interface IMotionCollisionPolicySource
    {
        /// <summary>是否携带了有效碰撞策略。为 false 时 solver 回退到 actor 默认约束。</summary>
        bool HasCollisionPolicy { get; }

        MotionCollisionConstraints CollisionPolicy { get; }
    }

    /// <summary>
    /// 可选接口：source 在本帧贡献最后一段位移并完成时，可声明仅用于该完成帧的碰撞策略。
    /// 典型用途是允许持续位移的中间帧处于障碍内，仅在最终落点执行重叠修正。
    /// </summary>
    public interface IMotionCompletionCollisionPolicySource
    {
        bool HasCompletionCollisionPolicy { get; }

        MotionCollisionConstraints CompletionCollisionPolicy { get; }
    }
}

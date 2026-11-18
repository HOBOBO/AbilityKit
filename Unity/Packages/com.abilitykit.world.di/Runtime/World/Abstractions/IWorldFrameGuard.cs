using System;

namespace AbilityKit.Ability.World.Abstractions
{
    /// <summary>
    /// 世界可选的帧完成门禁。宿主必须在推进前及提交后分别检查，
    /// 不能将异常隔离后的部分执行视为成功帧。
    /// </summary>
    public interface IWorldFrameGuard
    {
        /// <summary>
        /// 是否允许开始下一帧；待结算工作可在正常帧中处理。
        /// </summary>
        bool CanAdvance { get; }

        /// <summary>
        /// 是否已经完成本帧并可捕获或输出；检查必须包含两帧间新增的临时工作。
        /// </summary>
        bool CanCommit { get; }

        /// <summary>
        /// 进入世界执行，重复进入或已失败时抛出异常。
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// 全部执行与清理结束后检查完成条件；不满足时世界必须保持不可继续状态。
        /// </summary>
        void CompleteFrame();

        /// <summary>
        /// 保存失败原因并阻断后续推进与提交；不承担当前帧的自动撤销。
        /// </summary>
        void FailFrame(Exception error);
    }
}

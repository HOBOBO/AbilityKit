using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host
{
    /// <summary>
    /// 已结算输入流的可选执行端，只有实际执行的输入才参与预测对比。
    /// </summary>
    public interface IWorldSettledInputSink : IWorldClientInputSink
    {
        /// <summary>
        /// 提交本地预测并返回本帧真正进入模拟的有序输入，不写入权威账本。
        /// </summary>
        PlayerInputCommand[] ExecutePredictedInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);

        /// <summary>
        /// 模拟已校验结算帧中的执行项；无法执行时报告失败，不静默跳过。
        /// </summary>
        void ExecuteSettledInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> executedInputs);
    }

    /// <summary>
    /// 业务层已验证的结算记录。执行名单和元数据属于同一条不可替换记录；
    /// 来源在窥探与消费时返回同一实例，且不得修改命令负载或元数据。
    /// </summary>
    public sealed class WorldSettledInputFrame
    {
        /// <summary>
        /// 绑定已验证的帧号、实际执行名单及业务元数据。
        /// </summary>
        public WorldSettledInputFrame(FrameIndex frame, IReadOnlyList<PlayerInputCommand> executedInputs,
            IWorldSettledFrameMetadata metadata)
        {
            Frame = frame;
            ExecutedInputs = executedInputs ?? throw new ArgumentNullException(nameof(executedInputs));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        /// <summary>
        /// 权威记录所对应的模拟帧。
        /// </summary>
        public FrameIndex Frame { get; }

        /// <summary>
        /// 与权威派发顺序一致的执行名单，不含拒绝和重复命令。
        /// </summary>
        public IReadOnlyList<PlayerInputCommand> ExecutedInputs { get; }

        /// <summary>
        /// 与执行名单一同校验和消费的业务时间线。
        /// </summary>
        public IWorldSettledFrameMetadata Metadata { get; }
    }

    /// <summary>
    /// 由业务包解释的只读结算元数据，不是可调度的玩法命令。
    /// </summary>
    public interface IWorldSettledFrameMetadata { }

    /// <summary>
    /// 从上一个已应用的确认边界准备业务视图更新，不提前发布给预测或派发。
    /// </summary>
    public interface IWorldSettledTimelineSink : IWorldSettledInputSink
    {
        /// <summary>
        /// 在新客户端世界的帧零边界启用结算时间线，隔离旧输入来源。
        /// </summary>
        void BindSettledTimeline();

        /// <summary>
        /// 从上一个已确认边界预检下一帧，可失败但不得发布部分更新。
        /// </summary>
        IWorldSettledFrameUpdate PrepareSettledFrame(WorldSettledInputFrame frame);
    }

    /// <summary>
    /// 已预检的单帧更新。仅在确认时发布，不触发玩法、外发或再次校验。
    /// </summary>
    public interface IWorldSettledFrameUpdate
    {
        /// <summary>
        /// 元数据可能改变未确认的后续预测，须重新评估其原始输入。
        /// </summary>
        bool InvalidatesPrediction { get; }

        /// <summary>
        /// 一次性发布预检结果；调用方必须先验证世界帧完成及来源身份。
        /// </summary>
        void Commit();
    }

    public interface IWorldInputSink : IService
    {
        void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
    }

    /// <summary>
    /// Optional input boundary for rollback replay. The replay lifecycle lets a world distinguish
    /// a retransmitted network command from a deterministic re-execution of recorded input.
    /// </summary>
    public interface IWorldInputReplaySink : IWorldInputSink
    {
        void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame);
        void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
        void EndReplay();
    }

    /// <summary>
    /// 客户端驱动可选的来源感知入口。模拟收到的帧不等于在该客户端生成权威结果。
    /// 未实现此接口的世界仍使用普通输入入口。
    /// </summary>
    public interface IWorldClientInputSink : IWorldInputSink
    {
        /// <summary>
        /// 将该输入接收者绑定给客户端驱动，在世界的首个预测或接收帧之前调用。
        /// </summary>
        void BindClientInput();

        /// <summary>
        /// 提交本地暂态输入，不应产出模拟权威结果。
        /// </summary>
        void SubmitPredicted(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);

        /// <summary>
        /// 本地模拟收到的网络帧；权威结果本身须经独立的权威接收入口更新。
        /// </summary>
        void SubmitReceived(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
    }
}

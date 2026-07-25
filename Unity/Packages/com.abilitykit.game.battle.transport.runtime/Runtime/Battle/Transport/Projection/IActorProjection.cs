using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Transport.Projection
{
    /// <summary>
    /// 投影消费方接口。
    ///
    /// 无论是 snapshot 通道、预测通道、网关转发还是录像，都通过此接口消费
    /// 逻辑层的投影数据。消费方只读，不写回逻辑 world。
    ///
    /// 实现方：
    /// - <c>BattleSnapshotEntityApplier</c>（snapshot 通道 → view EntityWorld）
    /// - <c>PredictionViewBridge</c>（预测通道 → view EntityWorld）
    /// - <c>GatewayProjectionForwarder</c>（网关转发 → wire 序列化 → TCP 广播）
    /// - <c>ReplayProjectionRecorder</c>（录像记录，未来）
    /// </summary>
    public interface IActorProjectionConsumer
    {
        /// <summary>
        /// 处理一个 actor 的状态投影（创建或更新）。
        /// 消费方根据 <see cref="ActorProjectionData.Fields"/> 判断是创建还是更新
        /// （含 Spawn 标志 = 创建；不含 = 更新）。
        /// </summary>
        void ApplyActor(in ActorProjectionData data);

        /// <summary>
        /// 处理 actor 销毁。
        /// </summary>
        /// <param name="actorId">被销毁的 actor ID。</param>
        /// <param name="frame">销毁发生的帧。</param>
        void RemoveActor(int actorId, int frame);
    }

    /// <summary>
    /// 投影生产方接口。
    ///
    /// 从逻辑 world 提取 actor 状态为 <see cref="ActorProjectionData"/>。
    /// 实现方通常持有一个 <c>MobaActorRegistry</c> 或类似的 entity 查询能力。
    ///
    /// 关键价值：提取逻辑只有一份代码。所有消费方共享同一个生产方产出，
    /// 避免各自从 entity 提取导致字段遗漏/不一致（P0-1 哈希不一致的根因）。
    /// </summary>
    public interface IActorProjectionProducer
    {
        /// <summary>
        /// 提取指定 actor 的全量投影（含所有扩展层字段）。
        /// 用于状态同步 / 哈希校验。
        /// </summary>
        ActorProjectionData ExtractFull(int actorId);

        /// <summary>
        /// 提取所有存活 actor 的全量投影。
        /// 用于每帧状态同步推送。
        /// </summary>
        /// <param name="buffer">复用的缓冲区（避免每帧分配）。</param>
        void ExtractAll(List<ActorProjectionData> buffer);

        /// <summary>
        /// 提取指定 actor 的 Spawn 投影（含创建信息）。
        /// 用于 actor 创建时的首次推送。
        /// </summary>
        ActorProjectionData ExtractSpawn(int actorId);
    }
}

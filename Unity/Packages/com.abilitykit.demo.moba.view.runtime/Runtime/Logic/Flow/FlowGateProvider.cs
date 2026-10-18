#nullable enable
namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗准入 gate 的数据来源（per-battle-scope）。
    ///
    /// 迁移背景（见 MobaFlowSpec.md Step 3）：
    /// 这四个 gate 原本是 <c>GameFlowDomain</c> 上四个硬编码 <c>return true</c> 的私有方法
    /// （<c>IsAuthenticatedForFlow</c> / <c>IsRoomReadyForFlow</c> / <c>IsConnectivityReadyForFlow</c> /
    /// <c>IsAssetsReadyForFlow</c>），经 <c>BuildFlowConditionContext()</c> 注入 <c>MobaFlowConditionContext</c>，
    /// 最终合成 <c>BattleEntryReady</c> 决定能否进入战斗。
    ///
    /// 现改为 scope 内注册的服务来源：默认实现保持"全 true"（零行为变化），
    /// 后续接入真实鉴权/房间/连通性/资源就绪判定时，只需换 scope 内的实现，flow 侧无感。
    ///
    /// 语义说明：gate 是"能否进入战斗"的实时准入判定，每次转移求值时读取当前值，
    /// 因此用 Scoped 服务（每局一份、随 scope 释放）而非播种快照。
    ///
    /// 纯 C#（不引用 UnityEngine），可完整镜像进桌面 xUnit 工程做回归。
    /// </summary>
    public interface IFlowGateProvider
    {
        /// <summary>是否已通过鉴权（对应原 <c>IsAuthenticatedForFlow</c>）。</summary>
        bool IsAuthenticated { get; }

        /// <summary>房间是否就绪（对应原 <c>IsRoomReadyForFlow</c>）。</summary>
        bool IsRoomReady { get; }

        /// <summary>连通性是否就绪（对应原 <c>IsConnectivityReadyForFlow</c>）。</summary>
        bool IsConnectivityReady { get; }

        /// <summary>资源是否就绪（对应原 <c>IsAssetsReadyForFlow</c>）。</summary>
        bool IsAssetsReady { get; }
    }

    /// <summary>
    /// 默认 gate 实现：四项全 true，等价于迁移前四个硬编码 <c>return true</c> 方法，保证零行为变化。
    /// </summary>
    internal sealed class DefaultFlowGateProvider : IFlowGateProvider
    {
        public bool IsAuthenticated => true;

        public bool IsRoomReady => true;

        public bool IsConnectivityReady => true;

        public bool IsAssetsReady => true;
    }
}

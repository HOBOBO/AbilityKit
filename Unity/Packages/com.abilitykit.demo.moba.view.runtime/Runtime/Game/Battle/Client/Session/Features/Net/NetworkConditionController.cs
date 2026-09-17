using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 网络环境模拟控制器：在受控、可重复的条件下模拟各种网络环境
    /// （延迟、抖动、丢包、乱序），用于验证帧同步 / 预测回滚逻辑在不利网络下的表现。
    ///
    /// 该控制器不依赖任何第三方抓包或限速工具，所有上下行流量都经过
    /// <see cref="NetworkConditioningMiddleware"/> 受控投递，结果确定且可重放。
    ///
    /// 典型用法：
    /// <code>
    /// var controller = new NetworkConditionController();
    /// controller.Attach(gatewayConnection);          // 连接建立后挂载
    /// controller.ApplyProfile(NetworkConditionProfile.Mobile4G); // 切换到 4G 环境
    /// // 每帧由宿主循环驱动（若 ConnectionManager 已内置 Tick 钩子则无需手动驱动）
    /// </code>
    /// </summary>
    public sealed class NetworkConditionController
    {
        private ConnectionManager _connectionManager;
        private NetworkConditioningMiddleware _middleware;
        private NetworkConditionProfile _activeProfile;
        private NetworkConditionScenario _scenario;
        private bool _enabled;
        private int _seed;

        /// <summary>
        /// 当前是否已启用网络模拟。启用后上下行流量都会经过调理中间件。
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 当前生效的网络条件档案。
        /// </summary>
        public NetworkConditionProfile ActiveProfile => _activeProfile;

        /// <summary>
        /// 当前挂载的调理中间件实例；未挂载或已卸载时为 null。
        /// </summary>
        public NetworkConditioningMiddleware Middleware => _middleware;

        /// <summary>
        /// 绑定到指定连接。仅当连接底层为 <see cref="ConnectionManager"/> 时才能注入调理中间件。
        /// 可重复调用：若已绑定到同一连接则幂等；绑定到不同连接会先卸载旧的。
        /// </summary>
        /// <param name="connection">网关连接（通常是 <see cref="IBattleSessionGatewayConnectionFactory"/> 创建的实例）。</param>
        /// <returns>是否成功绑定（底层不是 ConnectionManager 时返回 false）。</returns>
        public bool Attach(IConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            var manager = connection as ConnectionManager;
            if (manager == null)
            {
                return false;
            }

            if (ReferenceEquals(_connectionManager, manager))
            {
                return true;
            }

            Detach();
            _connectionManager = manager;
            _connectionManager.PipelineCreated += OnPipelineCreated;
            _connectionManager.MiddlewareTick += OnMiddlewareTick;

            if (_enabled)
            {
                InstallMiddleware(_connectionManager.Pipeline);
            }

            return true;
        }

        /// <summary>
        /// 应用指定的网络条件档案。若当前未启用，会自动启用。
        /// 切换档案会重建中间件（保留种子以保证可重放性）。
        /// </summary>
        /// <param name="profile">要应用的网络条件。</param>
        public void ApplyProfile(NetworkConditionProfile profile)
        {
            _scenario = null;
            _activeProfile = profile;
            _enabled = true;

            // 重建中间件以应用新档案。若尚未 Attach，则仅记录档案，待 Attach 时生效。
            if (_connectionManager != null)
            {
                RebuildMiddleware();
            }
        }

        /// <summary>Apply time phases and optional direction/opcode overrides to the current connection.</summary>
        public void ApplyScenario(NetworkConditionScenario scenario)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _activeProfile = scenario.Baseline;
            _enabled = true;
            if (_connectionManager != null) RebuildMiddleware();
        }

        /// <summary>
        /// 使用预设档案快捷切换网络环境。
        /// </summary>
        /// <param name="preset">预设网络环境。</param>
        public void ApplyPreset(NetworkConditionPreset preset)
        {
            ApplyProfile(ResolvePreset(preset));
        }

        /// <summary>
        /// 设置确定性随机种子，用于抖动 / 丢包 / 乱序的可重放模拟。
        /// 修改种子会重建中间件。
        /// </summary>
        public void SetSeed(int seed)
        {
            _seed = seed;
            if (_connectionManager != null && _enabled)
            {
                RebuildMiddleware();
            }
        }

        /// <summary>
        /// 禁用网络模拟：移除调理中间件，恢复直通。
        /// 连接本身保持不变。
        /// </summary>
        public void Disable()
        {
            if (!_enabled) return;

            _enabled = false;
            RemoveMiddlewareFromPipeline();
            _middleware = null;
        }

        /// <summary>
        /// 卸载控制器：移除中间件并解除与连接的绑定。
        /// </summary>
        public void Detach()
        {
            if (_connectionManager == null) return;

            RemoveMiddlewareFromPipeline();
            _connectionManager.PipelineCreated -= OnPipelineCreated;
            _connectionManager.MiddlewareTick -= OnMiddlewareTick;
            _middleware = null;
            _connectionManager = null;
        }

        /// <summary>
        /// 获取当前调理统计（收发 / 投递 / 丢弃 / 乱序 / 待投递计数）。
        /// 未挂载中间件时返回全零统计。
        /// </summary>
        public NetworkConditioningStats GetStats()
        {
            return _middleware?.GetStats() ?? default;
        }

        public NetworkConditionDecision[] SnapshotDecisions()
        {
            return _middleware?.SnapshotDecisions() ?? Array.Empty<NetworkConditionDecision>();
        }

        private void RebuildMiddleware()
        {
            if (_connectionManager == null || !_enabled) return;

            RemoveMiddlewareFromPipeline();
            InstallMiddleware(_connectionManager.Pipeline);
        }

        private void InstallMiddleware(NetworkPipeline pipeline)
        {
            if (pipeline == null || !_enabled) return;

            _middleware = _scenario == null
                ? new NetworkConditioningMiddleware(_activeProfile, clockMs: null, seed: _seed,
                    decisionCapacity: 1024, maxPendingPackets: 4096)
                : new NetworkConditioningMiddleware(_scenario, clockMs: null, seed: _seed,
                    decisionCapacity: 1024, maxPendingPackets: 4096);
            pipeline.AddFirst(_middleware);
        }

        private void RemoveMiddlewareFromPipeline()
        {
            if (_middleware == null || _connectionManager == null) return;

            _connectionManager.Pipeline?.Remove(_middleware);
            _middleware.ClearPending();
        }

        private void OnPipelineCreated(NetworkPipeline pipeline)
        {
            _middleware?.ClearPending();
            _middleware = null;
            InstallMiddleware(pipeline);
        }

        private void OnMiddlewareTick(long nowMs)
        {
            _middleware?.Advance(nowMs);
        }

        private static NetworkConditionProfile ResolvePreset(NetworkConditionPreset preset)
        {
            switch (preset)
            {
                case NetworkConditionPreset.Ideal:
                    return NetworkConditionProfile.Ideal;
                case NetworkConditionPreset.Lan:
                    return NetworkConditionProfile.Lan;
                case NetworkConditionPreset.Mobile4G:
                    return NetworkConditionProfile.Mobile4G;
                case NetworkConditionPreset.CrossRegion:
                    return NetworkConditionProfile.CrossRegion;
                case NetworkConditionPreset.PoorWifi:
                    return NetworkConditionProfile.PoorWifi;
                case NetworkConditionPreset.LimitedBandwidth:
                    return NetworkConditionProfile.LimitedBandwidth;
                default:
                    return NetworkConditionProfile.Ideal;
            }
        }
    }

    /// <summary>
    /// 预设网络环境枚举，对应 <see cref="NetworkConditionProfile"/> 的内置预设。
    /// </summary>
    public enum NetworkConditionPreset
    {
        /// <summary>理想链路：零延迟、无丢包。作为对比基线。</summary>
        Ideal = 0,

        /// <summary>局域网：几毫秒延迟、无丢包。</summary>
        Lan = 1,

        /// <summary>典型 4G 移动网络：中等延迟 + 明显抖动。</summary>
        Mobile4G = 2,

        /// <summary>跨区域链路：高基础延迟、轻微抖动。</summary>
        CrossRegion = 3,

        /// <summary>差 WiFi：高抖动 + 可观丢包，同步模型的压力测试场景。</summary>
        PoorWifi = 4,

        /// <summary>带宽受限链路。</summary>
        LimitedBandwidth = 5,
    }
}

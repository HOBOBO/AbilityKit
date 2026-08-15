#nullable enable

using System;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Room
{
    /// <summary>Room 能力元数据绑定到同步会话后的状态。</summary>
    public enum RoomGatewayNetworkSyncBindingState
    {
        /// <summary>尚未执行能力绑定，或持有该状态的会话已经释放。</summary>
        Uninitialized = 0,

        /// <summary>接入方明确忽略远端能力。</summary>
        Ignored = 1,

        /// <summary>旧服务端未声明能力，会话按兼容策略只执行本地预检。</summary>
        LegacyFallback = 2,

        /// <summary>服务端已声明能力，并将参与同步会话协商。</summary>
        RemoteDeclared = 3,

        /// <summary>策略要求远端能力，但服务端没有提供声明；最终由会话构建器结构化拒绝。</summary>
        MissingRequired = 4
    }

    /// <summary>
    /// 将 Room 握手能力统一绑定到通用同步会话选项，避免各项目重复实现 Profile 校验和旧服回退规则。
    /// </summary>
    public readonly struct RoomGatewayNetworkSyncSessionBinding
    {
        private RoomGatewayNetworkSyncSessionBinding(
            RoomGatewayNetworkSyncBindingState state,
            RoomGatewayNetworkSyncCapabilities? declaration,
            NetworkSyncCapabilities? remoteCapabilities,
            NetworkSyncRemoteCapabilityPolicy policy)
        {
            State = state;
            Declaration = declaration;
            RemoteCapabilities = remoteCapabilities;
            Policy = policy;
        }

        public RoomGatewayNetworkSyncBindingState State { get; }

        /// <summary>经过版本和策略位校验的原始 Room 能力声明。</summary>
        public RoomGatewayNetworkSyncCapabilities? Declaration { get; }

        /// <summary>应传给通用同步会话构建器的远端能力。</summary>
        public NetworkSyncCapabilities? RemoteCapabilities { get; }

        public NetworkSyncRemoteCapabilityPolicy Policy { get; }

        public bool UsesRemoteCapabilities => State == RoomGatewayNetworkSyncBindingState.RemoteDeclared;

        public static RoomGatewayNetworkSyncSessionBinding Create(
            RoomGatewayNetworkSyncCapabilities? declaration,
            string expectedProfileName,
            NetworkSyncRemoteCapabilityPolicy policy = NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable)
        {
            if (!Enum.IsDefined(typeof(NetworkSyncRemoteCapabilityPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy), policy, "远端能力协商策略不是框架已知值。");
            }

            if (policy == NetworkSyncRemoteCapabilityPolicy.Ignore)
            {
                return new RoomGatewayNetworkSyncSessionBinding(
                    RoomGatewayNetworkSyncBindingState.Ignored,
                    declaration,
                    remoteCapabilities: null,
                    policy);
            }

            if (string.IsNullOrWhiteSpace(expectedProfileName))
            {
                throw new ArgumentException("绑定 Room 同步能力时必须提供预期 Profile 名称。", nameof(expectedProfileName));
            }

            if (declaration == null)
            {
                var state = policy == NetworkSyncRemoteCapabilityPolicy.Require
                    ? RoomGatewayNetworkSyncBindingState.MissingRequired
                    : RoomGatewayNetworkSyncBindingState.LegacyFallback;
                return new RoomGatewayNetworkSyncSessionBinding(
                    state,
                    declaration: null,
                    remoteCapabilities: null,
                    policy);
            }

            declaration.EnsureProfile(expectedProfileName);
            return new RoomGatewayNetworkSyncSessionBinding(
                RoomGatewayNetworkSyncBindingState.RemoteDeclared,
                declaration,
                declaration.Capabilities,
                policy);
        }

        /// <summary>把绑定结果应用到通用同步会话选项。</summary>
        public void ApplyTo(NetworkSyncSessionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.RemoteCapabilities = RemoteCapabilities;
            options.RemoteCapabilityPolicy = Policy;
        }
    }
}

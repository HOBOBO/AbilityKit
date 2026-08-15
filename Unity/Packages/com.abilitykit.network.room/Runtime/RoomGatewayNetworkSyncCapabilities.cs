#nullable enable

using System;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    /// <summary>房间协议同步能力元数据的解析错误。</summary>
    public enum RoomGatewaySyncCapabilityErrorCode
    {
        UnknownMetadataVersion = 0,
        InvalidCapabilities = 1,
        ProfileMismatch = 2
    }

    /// <summary>远端同步能力声明无法安全使用时抛出的结构化异常。</summary>
    public sealed class RoomGatewaySyncCapabilityException : InvalidOperationException
    {
        internal RoomGatewaySyncCapabilityException(
            RoomGatewaySyncCapabilityErrorCode errorCode,
            string message,
            NetworkSyncConfigurationReport? validationReport = null)
            : base(message)
        {
            ErrorCode = errorCode;
            ValidationReport = validationReport;
        }

        public RoomGatewaySyncCapabilityErrorCode ErrorCode { get; }

        public NetworkSyncConfigurationReport? ValidationReport { get; }
    }

    /// <summary>经过版本检查和策略位校验的远端同步能力声明。</summary>
    public sealed class RoomGatewayNetworkSyncCapabilities
    {
        internal RoomGatewayNetworkSyncCapabilities(
            int metadataVersion,
            string profileName,
            in NetworkSyncCapabilities capabilities)
        {
            MetadataVersion = metadataVersion;
            ProfileName = profileName ?? string.Empty;
            Capabilities = capabilities;
        }

        public int MetadataVersion { get; }

        public string ProfileName { get; }

        public NetworkSyncCapabilities Capabilities { get; }

        /// <summary>拒绝服务端与客户端选择了不同同步 Profile 的会话。</summary>
        public void EnsureProfile(string expectedProfileName)
        {
            if (string.Equals(ProfileName, expectedProfileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new RoomGatewaySyncCapabilityException(
                RoomGatewaySyncCapabilityErrorCode.ProfileMismatch,
                $"远端同步 Profile 不匹配。客户端={expectedProfileName}，服务端={ProfileName}。");
        }
    }

    /// <summary>将 Room wire DTO 转换为网络运行时能力对象。</summary>
    public static class RoomGatewayNetworkSyncCapabilitiesConverter
    {
        public const int CurrentMetadataVersion = 1;

        public static RoomGatewayNetworkSyncCapabilities? FromWire(WireNetworkSyncCapabilities? wire)
        {
            if (!wire.HasValue || wire.Value.MetadataVersion == 0)
            {
                return null;
            }

            var value = wire.Value;
            if (value.MetadataVersion != CurrentMetadataVersion)
            {
                throw new RoomGatewaySyncCapabilityException(
                    RoomGatewaySyncCapabilityErrorCode.UnknownMetadataVersion,
                    $"不支持远端同步能力元数据版本 {value.MetadataVersion}。");
            }

            var capabilities = new NetworkSyncCapabilities(
                value.MinimumSchemaVersion,
                value.MaximumSchemaVersion,
                (ClientPlaybackCapabilities)value.ClientPlayback,
                (InputPolicy)value.Input,
                (SnapshotPolicy)value.Snapshot,
                (InterestPolicy)value.Interest,
                (RecoveryPolicy)value.Recovery,
                (ServerValidationPolicy)value.ServerValidation,
                (ReliableEventCapabilities)value.ReliableEvent);
            var report = NetworkSyncConfigurationValidator.ValidateCapabilities(in capabilities);
            if (!report.IsValid)
            {
                throw new RoomGatewaySyncCapabilityException(
                    RoomGatewaySyncCapabilityErrorCode.InvalidCapabilities,
                    "远端同步能力声明未通过策略和版本校验。",
                    report);
            }

            return new RoomGatewayNetworkSyncCapabilities(
                value.MetadataVersion,
                value.ProfileName ?? string.Empty,
                in capabilities);
        }
    }
}

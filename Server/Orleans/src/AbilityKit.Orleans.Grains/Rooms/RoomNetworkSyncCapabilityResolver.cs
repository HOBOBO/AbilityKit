using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>根据服务端最终模板选择生成可公开给客户端的同步能力声明。</summary>
internal static class RoomNetworkSyncCapabilityResolver
{
    private const int MetadataVersion = 1;

    public static NetworkSyncCapabilityMetadata Resolve(
        RoomSummary summary,
        BattleInitParams initParams,
        string resolvedTemplateId)
    {
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        if (initParams is null) throw new ArgumentNullException(nameof(initParams));

        var profileName = ResolveProfileName(summary.RoomType, initParams.SyncOptions, resolvedTemplateId);
        var profile = ResolveProfile(summary.RoomType, initParams.SyncOptions, resolvedTemplateId);
        var (minimumSchemaVersion, maximumSchemaVersion) = ResolveSchemaRange(summary.RoomType, resolvedTemplateId);
        var capabilities = NetworkSyncCapabilities.FromProfile(
            in profile,
            minimumSchemaVersion,
            maximumSchemaVersion);
        return new NetworkSyncCapabilityMetadata(
            MetadataVersion,
            profileName,
            capabilities.MinimumSchemaVersion,
            capabilities.MaximumSchemaVersion,
            (int)capabilities.ClientPlayback,
            (int)capabilities.Input,
            (int)capabilities.Snapshot,
            (int)capabilities.Interest,
            (int)capabilities.Recovery,
            (int)capabilities.ServerValidation,
            (int)capabilities.ReliableEvent);
    }

    private static string ResolveProfileName(
        string roomType,
        BattleSyncStartOptions? syncOptions,
        string templateId)
    {
        if (GameplayRoomTypes.IsMoba(roomType))
        {
            return nameof(NetworkSyncModel.Lockstep);
        }

        var model = ResolveShooterModel(syncOptions, templateId);
        return NetworkSyncProfileRegistry.GetName(model);
    }

    private static NetworkSyncProfile ResolveProfile(
        string roomType,
        BattleSyncStartOptions? syncOptions,
        string templateId)
    {
        if (GameplayRoomTypes.IsMoba(roomType))
        {
            return NetworkSyncProfiles.Lockstep;
        }

        return NetworkSyncProfileRegistry.Resolve(ResolveShooterModel(syncOptions, templateId));
    }

    private static NetworkSyncModel ResolveShooterModel(
        BattleSyncStartOptions? syncOptions,
        string templateId)
    {
        if (syncOptions is not null &&
            Enum.IsDefined(typeof(NetworkSyncModel), syncOptions.SyncModel) &&
            syncOptions.SyncModel != (int)NetworkSyncModel.Unspecified)
        {
            return (NetworkSyncModel)syncOptions.SyncModel;
        }

        if (string.Equals(templateId, ShooterServerProtocol.AuthoritativeInterpolationPresentationTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.StateSyncAuthorityTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.PureStateAuthorityTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return NetworkSyncModel.AuthoritativeInterpolation;
        }

        if (string.Equals(templateId, ShooterServerProtocol.BatchStateLowFrequencyTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return NetworkSyncModel.BatchStateSync;
        }

        if (string.Equals(templateId, ShooterServerProtocol.MassBattleLodAoiTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return NetworkSyncModel.MassBattleLodSync;
        }

        if (string.Equals(templateId, ShooterServerProtocol.HybridHeroPredictionTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return NetworkSyncModel.HybridHeroPrediction;
        }

        return NetworkSyncModel.PredictRollback;
    }

    private static (int Minimum, int Maximum) ResolveSchemaRange(string roomType, string templateId)
    {
        if (!string.Equals(roomType, ShooterServerProtocol.RoomType, StringComparison.OrdinalIgnoreCase))
        {
            return (0, 1);
        }

        var usesPureState = string.Equals(templateId, ShooterServerProtocol.BatchStateLowFrequencyTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.MassBattleLodAoiTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.PureStateAuthorityTemplate, StringComparison.OrdinalIgnoreCase);
        return usesPureState
            ? (ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion, ShooterPureStateSyncCodec.CurrentVersion)
            : (1, ShooterPackedSnapshotCodec.CurrentVersion);
    }
}

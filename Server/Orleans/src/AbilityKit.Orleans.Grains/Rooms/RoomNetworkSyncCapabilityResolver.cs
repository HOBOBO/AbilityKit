using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>根据服务端最终模板选择生成可公开给客户端的同步能力声明。</summary>
internal static class RoomNetworkSyncCapabilityResolver
{
    private const int MetadataVersion = 1;

    public static NetworkSyncCapabilityMetadata Resolve(
        RoomSummary summary,
        BattleInitParams initParams,
        string resolvedTemplateId,
        ServerGameplayModuleCatalog? modules = null)
    {
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        if (initParams is null) throw new ArgumentNullException(nameof(initParams));

        var definition = (modules ?? ServerGameplayModuleCatalog.Default).ResolveSyncCapabilities(
            summary.RoomType, initParams.SyncOptions, resolvedTemplateId);
        var profile = definition.Profile;
        var capabilities = NetworkSyncCapabilities.FromProfile(
            in profile,
            definition.MinimumSchemaVersion,
            definition.MaximumSchemaVersion);
        return new NetworkSyncCapabilityMetadata(
            MetadataVersion,
            definition.ProfileName,
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
}

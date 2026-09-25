using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Gameplay;

internal static class ServerGameplaySyncCapabilityProfiles
{
    public static ServerSyncCapabilityDefinition ForMoba(BattleSyncStartOptions? _, string __)
    {
        return new ServerSyncCapabilityDefinition(
            nameof(NetworkSyncModel.Lockstep), NetworkSyncProfiles.Lockstep, 0, 1);
    }

    public static ServerSyncCapabilityDefinition ForShooter(
        BattleSyncStartOptions? syncOptions,
        string templateId)
    {
        var model = ResolveShooterModel(syncOptions, templateId);
        var profile = NetworkSyncProfileRegistry.Resolve(model);
        var (minimum, maximum) = ResolveShooterSchemaRange(templateId);
        return new ServerSyncCapabilityDefinition(
            NetworkSyncProfileRegistry.GetName(model), profile, minimum, maximum);
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

    private static (int Minimum, int Maximum) ResolveShooterSchemaRange(string templateId)
    {
        var usesPureState = string.Equals(templateId, ShooterServerProtocol.BatchStateLowFrequencyTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.MassBattleLodAoiTemplate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, ShooterServerProtocol.PureStateAuthorityTemplate, StringComparison.OrdinalIgnoreCase);
        return usesPureState
            ? (ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion, ShooterPureStateSyncCodec.CurrentVersion)
            : (1, ShooterPackedSnapshotCodec.CurrentVersion);
    }
}

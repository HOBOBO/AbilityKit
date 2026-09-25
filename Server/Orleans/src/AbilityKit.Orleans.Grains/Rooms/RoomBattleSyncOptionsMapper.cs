using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Rooms;

public static class RoomBattleSyncOptionsMapper
{
    public static BattleSyncStartOptions Resolve(RoomSummary summary, StartRoomBattleRequest request)
    {
        var requested = request.SyncOptions;
        return new BattleSyncStartOptions(
            FirstNonEmpty(requested?.SyncTemplateId, ReadTag(summary, RoomGatewaySyncTagKeys.SyncTemplateId)),
            requested?.SyncModel ?? ReadIntTag(summary, RoomGatewaySyncTagKeys.SyncModel, 0),
            FirstNonEmpty(requested?.NetworkEnvironmentId, ReadTag(summary, ShooterRoomTagKeys.NetworkEnvironmentId)),
            FirstNonEmpty(requested?.CarrierName, ReadTag(summary, ShooterRoomTagKeys.CarrierName)),
            requested?.EnableAuthoritativeWorld ?? ReadBoolTag(summary, ShooterRoomTagKeys.EnableAuthoritativeWorld, true),
            requested?.InterpolationEnabled ?? ReadBoolTag(summary, ShooterRoomTagKeys.InterpolationEnabled, false),
            requested?.InputDelayFrames ?? ReadIntTag(summary, RoomGatewaySyncTagKeys.InputDelayFrames, 0));
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }

    private static string? ReadTag(RoomSummary summary, string key)
    {
        return summary.Tags != null && summary.Tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static int ReadIntTag(RoomSummary summary, string key, int fallback)
    {
        return summary.Tags != null && summary.Tags.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBoolTag(RoomSummary summary, string key, bool fallback)
    {
        return summary.Tags != null && summary.Tags.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}

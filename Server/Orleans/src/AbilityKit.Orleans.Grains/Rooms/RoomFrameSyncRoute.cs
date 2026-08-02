using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplay;

namespace AbilityKit.Orleans.Grains.Rooms;

internal static class RoomFrameSyncRoute
{
    public static RoomBattleStartRoute ResolveStartRoute(RoomSummary summary, string battleId, BattleInitParams initParams)
    {
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        if (initParams is null) throw new ArgumentNullException(nameof(initParams));

        var syncProfile = ServerGameplayModuleCatalog.Default.ResolveSyncProfile(summary.RoomType);
        if (!syncProfile.TryResolveTemplate(initParams.SyncOptions?.SyncTemplateId, out var syncTemplate))
        {
            return new RoomBattleStartRoute(true, null, syncProfile.DefaultTemplateId, true);
        }

        if (!syncTemplate.SupportsFrameSync)
        {
            return new RoomBattleStartRoute(syncTemplate.RequiresBattleRuntime, null, syncTemplate.TemplateId, false);
        }

        var roomId = initParams.WorldId;
        var frameSyncOptions = new FrameSyncStartOptions(
            roomId,
            initParams.WorldId,
            initParams.TickRate > 0 ? initParams.TickRate : 30,
            battleId,
            syncTemplate.TemplateId,
            (int)syncTemplate.RuntimeMode);

        return new RoomBattleStartRoute(syncTemplate.RequiresBattleRuntime, frameSyncOptions, syncTemplate.TemplateId, false);
    }

    public static FrameSyncStartOptions? Resolve(RoomSummary summary, string battleId, BattleInitParams initParams)
    {
        return ResolveStartRoute(summary, battleId, initParams).FrameSyncOptions;
    }
}

internal sealed record RoomBattleStartRoute(
    bool RequiresBattleRuntime,
    FrameSyncStartOptions? FrameSyncOptions,
    string SyncTemplateId,
    bool IsUnsupportedTemplate);

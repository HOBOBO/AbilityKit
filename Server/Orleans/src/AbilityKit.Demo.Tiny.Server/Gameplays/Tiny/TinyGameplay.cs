using AbilityKit.Demo.Tiny;

namespace AbilityKit.Orleans.Grains.Gameplays.Tiny;

public static class TinyGameplay
{
    public const string RoomType = "tiny";
    public const string WorldType = TinySyncTemplates.WorldType;
    public const string StateSyncTemplate = TinySyncTemplates.State;
    public const string FrameSyncTemplate = TinySyncTemplates.Frame;
    public const string HybridSyncTemplate = TinySyncTemplates.Hybrid;
    public const int TickRate = TinySyncSettings.TickRate;
}

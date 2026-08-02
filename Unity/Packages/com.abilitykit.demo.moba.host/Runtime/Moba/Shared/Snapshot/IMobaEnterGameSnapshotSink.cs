using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Extensions.Moba.Snapshot
{
    /// <summary>
    /// Publishes the serialized EnterGame snapshot payload produced by a battle start flow.
    /// </summary>
    public interface IMobaEnterGameSnapshotSink
    {
        void PublishEnterGameResPayload(byte[] payload);
    }

    /// <summary>
    /// Exposes the EnterGame snapshot as a host-level runtime snapshot source.
    /// </summary>
    public interface IMobaEnterGameSnapshotSource
    {
        bool TryGetEnterGameSnapshot(out WorldStateSnapshot snapshot);
    }
}

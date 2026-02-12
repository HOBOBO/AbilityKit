using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public enum FrameSyncCatchUpMessageKind
    {
        Unknown = 0,
        CatchUpRequest = 1,
        CatchUpPayload = 2,
    }

    public readonly struct FrameSyncCatchUpRequestMessage
    {
        public readonly string ClientId;
        public readonly FrameSyncCatchUpRequest Request;

        public FrameSyncCatchUpRequestMessage(string clientId, in FrameSyncCatchUpRequest request)
        {
            ClientId = clientId;
            Request = request;
        }

        public static FrameSyncCatchUpRequestMessage FromFrames(string clientId, WorldId worldId, int fromFrameExclusive, int toFrameInclusive)
        {
            return new FrameSyncCatchUpRequestMessage(clientId, new FrameSyncCatchUpRequest(worldId, new AbilityKit.Ability.FrameSync.FrameIndex(fromFrameExclusive), new AbilityKit.Ability.FrameSync.FrameIndex(toFrameInclusive)));
        }
    }

    public readonly struct FrameSyncCatchUpPayloadMessage
    {
        public readonly string ClientId;
        public readonly FrameSyncCatchUpPayload Payload;

        public FrameSyncCatchUpPayloadMessage(string clientId, in FrameSyncCatchUpPayload payload)
        {
            ClientId = clientId;
            Payload = payload;
        }
    }
}

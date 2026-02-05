using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Game.Flow.Snapshot
{
    internal static class LobbySnapshotDeclarations
    {
        [SnapshotDecoder("lobby", (int)MobaOpCode.LobbySnapshot, typeof(LobbySnapshot))]
        internal static bool DecodeLobby(in WorldStateSnapshot snap, out LobbySnapshot lobby)
        {
            if (snap.Payload == null || snap.Payload.Length == 0)
            {
                lobby = default;
                return false;
            }

            lobby = MobaLobbyCodec.DeserializeSnapshot(snap.Payload);
            return true;
        }
    }
}

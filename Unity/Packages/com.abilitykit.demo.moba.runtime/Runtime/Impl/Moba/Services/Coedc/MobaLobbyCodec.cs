using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaLobbyCodec
    {
        public static byte[] SerializeSnapshot(bool started, PlayerReadyEntry[] players, int version)
        {
            var list = players ?? Array.Empty<PlayerReadyEntry>();
            var snapshot = new LobbySnapshot(started, list, version);
            return BinaryObjectCodec.Encode(snapshot);
        }

        public static LobbySnapshot DeserializeSnapshot(byte[] payload)
        {
            return BinaryObjectCodec.Decode<LobbySnapshot>(payload);
        }
    }

    public readonly struct PlayerReadyEntry
    {
        [BinaryMember(0)] public readonly PlayerId PlayerId;
        [BinaryMember(1)] public readonly bool Ready;

        public PlayerReadyEntry(PlayerId playerId, bool ready)
        {
            PlayerId = playerId;
            Ready = ready;
        }
    }

    public readonly struct LobbySnapshot
    {
        [BinaryMember(1)] public readonly bool Started;
        [BinaryMember(2)] public readonly PlayerReadyEntry[] Players;
        [BinaryMember(0)] public readonly int Version;

        public LobbySnapshot(bool started, PlayerReadyEntry[] players, int version)
        {
            Started = started;
            Players = players;
            Version = version;
        }
    }
}

using System;
using System.IO;
using System.Text;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaLobbyCodec
    {
        public static byte[] SerializeSnapshot(bool started, PlayerReadyEntry[] players, int version)
        {
            using var ms = new MemoryStream(256);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(version);
            bw.Write(started);

            var list = players ?? Array.Empty<PlayerReadyEntry>();
            bw.Write(list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                WriteString(bw, list[i].PlayerId.Value);
                bw.Write(list[i].Ready);
            }

            bw.Flush();
            return ms.ToArray();
        }

        public static LobbySnapshot DeserializeSnapshot(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var version = br.ReadInt32();
            var started = br.ReadBoolean();
            var count = br.ReadInt32();
            var players = count > 0 ? new PlayerReadyEntry[count] : Array.Empty<PlayerReadyEntry>();
            for (int i = 0; i < count; i++)
            {
                var pid = new PlayerId(ReadString(br));
                var ready = br.ReadBoolean();
                players[i] = new PlayerReadyEntry(pid, ready);
            }

            return new LobbySnapshot(started, players, version);
        }

        private static void WriteString(BinaryWriter bw, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                bw.Write(0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(s);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        private static string ReadString(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return string.Empty;
            var bytes = br.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public readonly struct PlayerReadyEntry
    {
        public readonly PlayerId PlayerId;
        public readonly bool Ready;

        public PlayerReadyEntry(PlayerId playerId, bool ready)
        {
            PlayerId = playerId;
            Ready = ready;
        }
    }

    public readonly struct LobbySnapshot
    {
        public readonly bool Started;
        public readonly PlayerReadyEntry[] Players;
        public readonly int Version;

        public LobbySnapshot(bool started, PlayerReadyEntry[] players, int version)
        {
            Started = started;
            Players = players;
            Version = version;
        }
    }
}

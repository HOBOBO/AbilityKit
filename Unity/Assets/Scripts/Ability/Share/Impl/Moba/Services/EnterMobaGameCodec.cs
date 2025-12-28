using System;
using System.IO;
using System.Text;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class EnterMobaGameCodec
    {
        public static byte[] SerializeReq(in EnterMobaGameReq req)
        {
            using var ms = new MemoryStream(256);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            WriteString(bw, req.PlayerId.Value);
            WriteString(bw, req.MatchId);
            bw.Write(req.MapId);
            bw.Write(req.TeamId);
            bw.Write(req.HeroId);
            bw.Write(req.RandomSeed);
            bw.Write(req.TickRate);
            bw.Write(req.InputDelayFrames);
            bw.Write(req.OpCode);
            WriteBytes(bw, req.Payload);

            bw.Flush();
            return ms.ToArray();
        }

        public static EnterMobaGameReq DeserializeReq(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var playerId = new PlayerId(ReadString(br));
            var matchId = ReadString(br);
            var mapId = br.ReadInt32();
            var teamId = br.ReadInt32();
            var heroId = br.ReadInt32();
            var randomSeed = br.ReadInt32();
            var tickRate = br.ReadInt32();
            var inputDelayFrames = br.ReadInt32();
            var opCode = br.ReadInt32();
            var payload = ReadBytes(br);

            return new EnterMobaGameReq(playerId, matchId, mapId, teamId, heroId, randomSeed, tickRate, inputDelayFrames, opCode, payload);
        }

        public static byte[] SerializeRes(in EnterMobaGameRes res)
        {
            using var ms = new MemoryStream(256);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            WriteString(bw, res.WorldId.Value);
            WriteString(bw, res.PlayerId.Value);
            bw.Write(res.LocalActorId);
            bw.Write(res.RandomSeed);
            bw.Write(res.TickRate);
            bw.Write(res.InputDelayFrames);

            var players = res.Players ?? Array.Empty<MobaPlayerEntry>();
            bw.Write(players.Length);
            for (int i = 0; i < players.Length; i++)
            {
                WriteString(bw, players[i].PlayerId.Value);
                bw.Write(players[i].TeamId);
                bw.Write(players[i].HeroId);
                bw.Write(players[i].SpawnIndex);
            }

            bw.Write(res.OpCode);
            WriteBytes(bw, res.Payload);

            bw.Flush();
            return ms.ToArray();
        }

        public static EnterMobaGameRes DeserializeRes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var worldId = new WorldId(ReadString(br));
            var playerId = new PlayerId(ReadString(br));
            var localActorId = br.ReadInt32();
            var randomSeed = br.ReadInt32();
            var tickRate = br.ReadInt32();
            var inputDelayFrames = br.ReadInt32();

            var count = br.ReadInt32();
            var players = count > 0 ? new MobaPlayerEntry[count] : Array.Empty<MobaPlayerEntry>();
            for (int i = 0; i < count; i++)
            {
                var pid = new PlayerId(ReadString(br));
                var teamId = br.ReadInt32();
                var heroId = br.ReadInt32();
                var spawnIndex = br.ReadInt32();
                players[i] = new MobaPlayerEntry(pid, teamId, heroId, spawnIndex);
            }

            var opCode = br.ReadInt32();
            var payload = ReadBytes(br);

            return new EnterMobaGameRes(worldId, playerId, localActorId, randomSeed, tickRate, inputDelayFrames, players, opCode, payload);
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

        private static void WriteBytes(BinaryWriter bw, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                bw.Write(0);
                return;
            }

            bw.Write(data.Length);
            bw.Write(data);
        }

        private static byte[] ReadBytes(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return Array.Empty<byte>();
            return br.ReadBytes(len);
        }
    }
}

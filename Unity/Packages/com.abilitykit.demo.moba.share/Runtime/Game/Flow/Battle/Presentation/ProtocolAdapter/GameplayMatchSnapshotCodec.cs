using System;
using System.IO;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayMatchSnapshotCodec
    {
        public const int OpCode = MobaOpCodes.Snapshot.GameplayMatch;
        private const int Version = 2;
        private const int MaxPayloadLength = 2048;

        public static byte[] Encode(in GameplayMatchData state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(state.GameplayId);
                writer.Write((int)state.Phase);
                writer.Write(state.Team1Score);
                writer.Write(state.Team2Score);
                writer.Write(state.WinTeamId);
                writer.Write(state.EndFrame);
                writer.Write(state.EndReason ?? string.Empty);
                writer.Write(state.MatchId ?? string.Empty);
                writer.Write(state.SnapshotFrame);
                if (stream.Length > MaxPayloadLength)
                    throw new InvalidOperationException("Gameplay match snapshot is too large.");
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] payload, out GameplayMatchData state)
        {
            state = default;
            if (payload == null || payload.Length < 37 || payload.Length > MaxPayloadLength) return false;
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream))
                {
                    var version = reader.ReadInt32();
                    if (version != 1 && version != Version) return false;
                    var gameplayId = reader.ReadInt32();
                    var phase = reader.ReadInt32();
                    var team1Score = reader.ReadDouble();
                    var team2Score = reader.ReadDouble();
                    var winTeamId = reader.ReadInt32();
                    var endFrame = reader.ReadInt32();
                    var endReason = reader.ReadString();
                    var matchId = version == Version ? reader.ReadString() : string.Empty;
                    var snapshotFrame = version == Version ? reader.ReadInt32() : 0;
                    if (stream.Position != stream.Length || gameplayId < 0 || phase < 0 || phase > 3 ||
                        snapshotFrame < 0 ||
                        double.IsNaN(team1Score) || double.IsInfinity(team1Score) ||
                        double.IsNaN(team2Score) || double.IsInfinity(team2Score) ||
                        team1Score < 0 || team2Score < 0 || winTeamId < 0 || winTeamId > 2 ||
                        endFrame < 0 || (phase != (int)GameplayClockPhase.Ended &&
                        (winTeamId != 0 || endFrame != 0 || endReason.Length != 0))) return false;
                    state = new GameplayMatchData(gameplayId, (GameplayClockPhase)phase,
                        team1Score, team2Score, winTeamId, endFrame, endReason,
                        matchId, snapshotFrame);
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}

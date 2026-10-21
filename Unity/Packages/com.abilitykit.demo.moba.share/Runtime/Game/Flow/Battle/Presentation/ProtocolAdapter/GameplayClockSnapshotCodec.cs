using System.IO;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayClockSnapshotCodec
    {
        public const int OpCode = 4014;
        private const int Version = 3;
        private const int PayloadLength = sizeof(int) * 5;

        public static byte[] Encode(in GameplayClockData clock)
        {
            using (var stream = new MemoryStream(PayloadLength))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(clock.GameplayId);
                writer.Write((int)clock.Phase);
                writer.Write(clock.DurationMilliseconds);
                writer.Write(clock.RemainingMilliseconds);
                writer.Write(clock.ElapsedMilliseconds);
                writer.Write(clock.MatchId ?? string.Empty);
                writer.Write(clock.SnapshotFrame);
                if (stream.Length > 2048)
                    throw new System.InvalidOperationException("Gameplay clock snapshot is too large.");
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] payload, out GameplayClockData clock)
        {
            clock = default;
            if (payload == null || payload.Length < PayloadLength || payload.Length > 2048) return false;

            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                var version = reader.ReadInt32();
                if (version < 1 || version > Version) return false;
                var gameplayId = reader.ReadInt32();
                var phase = reader.ReadInt32();
                var duration = reader.ReadInt32();
                var remaining = reader.ReadInt32();
                var elapsed = 0;
                string matchId;
                int snapshotFrame;
                try
                {
                    if (version >= 3) elapsed = reader.ReadInt32();
                    matchId = version >= 2 ? reader.ReadString() : string.Empty;
                    snapshotFrame = version >= 2 ? reader.ReadInt32() : 0;
                }
                catch (EndOfStreamException) { return false; }
                if (gameplayId < 0 || phase < 0 || phase > 3 || duration < 0 ||
                    remaining < 0 || (duration > 0 && remaining > duration) || elapsed < 0 || snapshotFrame < 0 ||
                    stream.Position != stream.Length) return false;
                clock = new GameplayClockData(gameplayId, (GameplayClockPhase)phase, duration, remaining,
                    matchId, snapshotFrame, elapsed);
                return true;
            }
        }
    }
}

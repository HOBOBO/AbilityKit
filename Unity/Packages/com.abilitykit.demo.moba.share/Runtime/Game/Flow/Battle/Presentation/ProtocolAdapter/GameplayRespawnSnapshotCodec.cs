using System;
using System.IO;

namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayRespawnSnapshotCodec
    {
        public const int OpCode = 4016;
        private const int Version = 1;
        private const int MaxEntries = 256;

        public static byte[] Encode(GameplayRespawnData[] entries)
        {
            entries = entries ?? Array.Empty<GameplayRespawnData>();
            if (entries.Length > MaxEntries) throw new ArgumentOutOfRangeException(nameof(entries));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(entries.Length);
                foreach (var entry in entries)
                {
                    writer.Write(entry.ActorId);
                    writer.Write(entry.RemainingMilliseconds);
                }
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] payload, out GameplayRespawnData[] entries)
        {
            entries = null;
            if (payload == null || payload.Length < 8 || payload.Length > 8 + MaxEntries * 8)
                return false;
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Version) return false;
                    var count = reader.ReadInt32();
                    if (count < 0 || count > MaxEntries || payload.Length != 8 + count * 8) return false;
                    var result = new GameplayRespawnData[count];
                    var previousActorId = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var actorId = reader.ReadInt32();
                        var remaining = reader.ReadInt32();
                        if (actorId <= previousActorId || remaining < 0) return false;
                        result[i] = new GameplayRespawnData(actorId, remaining);
                        previousActorId = actorId;
                    }
                    entries = result;
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

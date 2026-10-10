using System;
using System.IO;

namespace AbilityKit.Demo.Moba.Share
{
    public static class CharacterActionSnapshotCodec
    {
        public const int OpCode = 4017;
        private const int Version = 1;
        private const int MaxEntries = 128;
        private const int MaxPayloadLength = 32768;

        public static byte[] Encode(CharacterActionData[] entries)
        {
            entries ??= Array.Empty<CharacterActionData>();
            if (entries.Length > MaxEntries) throw new InvalidOperationException("Too many character actions.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(entries.Length);
                var previousActorId = 0;
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (!IsValid(in entry) || entry.ActorId <= previousActorId)
                        throw new InvalidOperationException("Invalid character action.");
                    previousActorId = entry.ActorId;
                    writer.Write(entry.ActorId);
                    writer.Write(entry.EntityVersion);
                    writer.Write(entry.SnapshotFrame);
                    writer.Write(entry.Path);
                    writer.Write(entry.InstanceId);
                    writer.Write(entry.StartFrame);
                    writer.Write(entry.LocalFrame);
                    writer.Write(entry.CastInstanceId);
                    writer.Write(entry.SkillId);
                }
                if (stream.Length > MaxPayloadLength)
                    throw new InvalidOperationException("Character action snapshot is too large.");
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] payload, out CharacterActionData[] entries)
        {
            entries = Array.Empty<CharacterActionData>();
            if (payload == null || payload.Length < 8 || payload.Length > MaxPayloadLength) return false;
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Version) return false;
                    var count = reader.ReadInt32();
                    if (count < 0 || count > MaxEntries) return false;
                    var decoded = new CharacterActionData[count];
                    var previousActorId = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var entry = new CharacterActionData(reader.ReadInt32(), reader.ReadInt32(),
                            reader.ReadInt32(), reader.ReadString(), reader.ReadInt64(),
                            reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32());
                        if (!IsValid(in entry) || entry.ActorId <= previousActorId) return false;
                        previousActorId = entry.ActorId;
                        decoded[i] = entry;
                    }
                    if (stream.Position != stream.Length) return false;
                    entries = decoded;
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static bool IsValid(in CharacterActionData entry) =>
            entry.ActorId > 0 && entry.EntityVersion > 0 && entry.SnapshotFrame >= 0 &&
            !string.IsNullOrEmpty(entry.Path) && entry.Path.Length <= 128 &&
            entry.InstanceId > 0 && entry.StartFrame >= 0 &&
            entry.StartFrame <= entry.SnapshotFrame &&
            entry.LocalFrame == entry.SnapshotFrame - entry.StartFrame &&
            entry.CastInstanceId >= 0 && entry.SkillId >= 0;
    }
}

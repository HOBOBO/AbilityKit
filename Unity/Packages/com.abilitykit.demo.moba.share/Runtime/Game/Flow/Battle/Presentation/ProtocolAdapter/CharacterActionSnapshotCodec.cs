using System;
using System.IO;

namespace AbilityKit.Demo.Moba.Share
{
    public static class CharacterActionSnapshotCodec
    {
        public const int OpCode = 4017;
        private const int LegacyVersion = 1;
        private const int Version = 3;
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
                    writer.Write(entry.ModeId ?? string.Empty);
                    writer.Write(entry.ModeInstanceId);
                    writer.Write(entry.ModeStartFrame);
                    writer.Write(entry.ModeLocalFrame);
                    writer.Write(entry.CastFrame.HasValue);
                    if (entry.CastFrame.HasValue)
                    {
                        writer.Write(entry.CastFrame.OriginX); writer.Write(entry.CastFrame.OriginY); writer.Write(entry.CastFrame.OriginZ);
                        writer.Write(entry.CastFrame.ForwardX); writer.Write(entry.CastFrame.ForwardY); writer.Write(entry.CastFrame.ForwardZ);
                    }
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
                    var version = reader.ReadInt32();
                    if (version != LegacyVersion && version != 2 && version != Version) return false;
                    var count = reader.ReadInt32();
                    if (count < 0 || count > MaxEntries) return false;
                    var decoded = new CharacterActionData[count];
                    var previousActorId = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var actorId = reader.ReadInt32();
                        var entityVersion = reader.ReadInt32();
                        var snapshotFrame = reader.ReadInt32();
                        var path = reader.ReadString();
                        var instanceId = reader.ReadInt64();
                        var startFrame = reader.ReadInt32();
                        var localFrame = reader.ReadInt32();
                        var castInstanceId = reader.ReadInt64();
                        var skillId = reader.ReadInt32();
                        var entry = version == LegacyVersion
                            ? new CharacterActionData(actorId, entityVersion, snapshotFrame, path,
                                instanceId, startFrame, localFrame, castInstanceId, skillId)
                            : new CharacterActionData(actorId, entityVersion, snapshotFrame, path,
                                instanceId, startFrame, localFrame, castInstanceId, skillId,
                                reader.ReadString(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32());
                        if (version >= 3)
                        {
                            var flag = reader.ReadByte();
                            if (flag > 1) return false;
                            var castFrame = flag == 0 ? default : new CastPresentationFrame(
                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            entry = new CharacterActionData(actorId, entityVersion, snapshotFrame, path,
                                instanceId, startFrame, localFrame, castInstanceId, skillId, entry.ModeId,
                                entry.ModeInstanceId, entry.ModeStartFrame, entry.ModeLocalFrame, castFrame);
                        }
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
            entry.CastInstanceId >= 0 && entry.SkillId >= 0 &&
            !string.IsNullOrEmpty(entry.ModeId) && entry.ModeId.Length <= 64 &&
            entry.ModeInstanceId >= 0 && entry.ModeStartFrame >= 0 &&
            entry.ModeStartFrame <= entry.SnapshotFrame &&
            entry.ModeLocalFrame == entry.SnapshotFrame - entry.ModeStartFrame &&
            entry.CastFrame.IsValid && (!entry.CastFrame.HasValue || entry.CastInstanceId > 0 && entry.SkillId > 0);
    }
}

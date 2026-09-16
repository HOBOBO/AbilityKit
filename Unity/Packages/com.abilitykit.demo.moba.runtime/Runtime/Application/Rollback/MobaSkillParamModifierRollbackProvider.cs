using System;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaSkillParamModifierRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10015;
        private const int Version = 1;
        private readonly MobaSkillParamModifierService _modifiers;

        public MobaSkillParamModifierRollbackProvider(MobaSkillParamModifierService modifiers)
        {
            _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        }

        public int Key => DefaultKey;
        public string Name => "SkillParamModifiers";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _modifiers.CaptureRollbackSnapshot();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            var entries = snapshot.Entries ?? Array.Empty<MobaSkillParamModifierSnapshotEntry>();
            writer.Write(entries.Length);
            for (var i = 0; i < entries.Length; i++) WriteEntry(writer, in entries[i]);
            writer.Flush();
            return stream.ToArray();
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _modifiers.RestoreRollbackSnapshot(default);
                return;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            var version = reader.ReadInt32();
            if (version != Version) throw new InvalidOperationException($"Unsupported skill parameter modifier rollback payload version '{version}'.");
            var count = reader.ReadInt32();
            if (count < 0 || count > 1_000_000) throw new InvalidDataException($"Invalid skill parameter modifier count '{count}'.");
            var entries = new MobaSkillParamModifierSnapshotEntry[count];
            for (var i = 0; i < count; i++) entries[i] = ReadEntry(reader);
            if (stream.Position != stream.Length) throw new InvalidDataException("Skill parameter modifier rollback payload contains trailing data.");
            _modifiers.RestoreRollbackSnapshot(new MobaSkillParamModifierServiceSnapshot(entries));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static void WriteEntry(BinaryWriter writer, in MobaSkillParamModifierSnapshotEntry entry)
        {
            writer.Write((int)entry.Scope);
            writer.Write(entry.OwnerId);
            var modifier = entry.Modifier;
            writer.Write(modifier.Key.Packed);
            writer.Write((byte)modifier.Op);
            writer.Write(modifier.Priority);
            writer.Write(modifier.SourceId);
            writer.Write(modifier.SourceNameIndex);
            WriteMagnitude(writer, in modifier.Magnitude);
            writer.Write(modifier.Metadata.SourceNameIndex);
            writer.Write(modifier.Metadata.TagsMask);
            writer.Write(modifier.Metadata.CreatedTime);
            writer.Write(modifier.Metadata.OwnerId);
            writer.Write(modifier.CustomData.CustomTypeId);
            writer.Write(modifier.CustomData.IntValue);
            WriteNullableString(writer, modifier.CustomData.StringValue);
            WriteBytes(writer, modifier.CustomData.RawData);
        }

        private static MobaSkillParamModifierSnapshotEntry ReadEntry(BinaryReader reader)
        {
            var scope = (MobaModifierOwnerScope)reader.ReadInt32();
            var ownerId = reader.ReadInt32();
            var modifier = new ModifierData
            {
                Key = ModifierKey.FromPacked(reader.ReadUInt32()),
                Op = (ModifierOp)reader.ReadByte(),
                Priority = reader.ReadInt32(),
                SourceId = reader.ReadInt32(),
                SourceNameIndex = reader.ReadInt16(),
                Magnitude = ReadMagnitude(reader),
                Metadata = new ModifierMetadata
                {
                    SourceNameIndex = reader.ReadInt16(),
                    TagsMask = reader.ReadUInt32(),
                    CreatedTime = reader.ReadSingle(),
                    OwnerId = reader.ReadInt32(),
                },
                CustomData = new CustomModifierData
                {
                    CustomTypeId = reader.ReadInt32(),
                    IntValue = reader.ReadInt32(),
                    StringValue = ReadNullableString(reader),
                    RawData = ReadBytes(reader),
                },
            };
            return new MobaSkillParamModifierSnapshotEntry(scope, ownerId, modifier);
        }

        private static void WriteMagnitude(BinaryWriter writer, in MagnitudeSource magnitude)
        {
            writer.Write((byte)magnitude.Type);
            writer.Write(magnitude.Data0);
            writer.Write(magnitude.Data1);
            writer.Write(magnitude.Data2);
            WriteFloats(writer, magnitude.ArrayData);
            var pipeline = magnitude.PipelineData;
            writer.Write(pipeline.Count);
            WriteSingleModifier(writer, in pipeline.Modifier0);
            WriteSingleModifier(writer, in pipeline.Modifier1);
            WriteSingleModifier(writer, in pipeline.Modifier2);
            WriteSingleModifier(writer, in pipeline.Modifier3);
        }

        private static MagnitudeSource ReadMagnitude(BinaryReader reader)
        {
            var result = new MagnitudeSource
            {
                Type = (MagnitudeSourceType)reader.ReadByte(),
                Data0 = reader.ReadSingle(),
                Data1 = reader.ReadSingle(),
                Data2 = reader.ReadSingle(),
                ArrayData = ReadFloats(reader),
            };
            result.PipelineData = new MagnitudePipelineData
            {
                Count = reader.ReadByte(),
                Modifier0 = ReadSingleModifier(reader),
                Modifier1 = ReadSingleModifier(reader),
                Modifier2 = ReadSingleModifier(reader),
                Modifier3 = ReadSingleModifier(reader),
            };
            return result;
        }

        private static void WriteSingleModifier(BinaryWriter writer, in SingleModifierData modifier)
        {
            writer.Write(modifier.TypeId);
            writer.Write(modifier.Param0);
            writer.Write(modifier.Param1);
            writer.Write(modifier.Param2);
            writer.Write(modifier.Param3);
            WriteFloats(writer, modifier.Curve);
        }

        private static SingleModifierData ReadSingleModifier(BinaryReader reader)
        {
            return new SingleModifierData
            {
                TypeId = reader.ReadByte(),
                Param0 = reader.ReadSingle(),
                Param1 = reader.ReadSingle(),
                Param2 = reader.ReadSingle(),
                Param3 = reader.ReadSingle(),
                Curve = ReadFloats(reader),
            };
        }

        private static void WriteFloats(BinaryWriter writer, float[] values)
        {
            if (values == null) { writer.Write(-1); return; }
            writer.Write(values.Length);
            for (var i = 0; i < values.Length; i++) writer.Write(values[i]);
        }

        private static float[] ReadFloats(BinaryReader reader)
        {
            var count = ReadCollectionCount(reader);
            if (count < 0) return null;
            var result = new float[count];
            for (var i = 0; i < count; i++) result[i] = reader.ReadSingle();
            return result;
        }

        private static void WriteBytes(BinaryWriter writer, byte[] values)
        {
            if (values == null) { writer.Write(-1); return; }
            writer.Write(values.Length);
            writer.Write(values);
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            var count = ReadCollectionCount(reader);
            if (count < 0) return null;
            var result = reader.ReadBytes(count);
            if (result.Length != count) throw new EndOfStreamException();
            return result;
        }

        private static int ReadCollectionCount(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < -1 || count > 1_000_000) throw new InvalidDataException($"Invalid collection count '{count}'.");
            return count;
        }

        private static void WriteNullableString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private static string ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaTriggerModifierContinuousRollbackProvider : IRollbackStateProvider,
        IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10032;
        private const int Version = 1;
        private readonly MobaTriggerModifierContinuousService _service;

        public MobaTriggerModifierContinuousRollbackProvider(MobaTriggerModifierContinuousService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public int Key => DefaultKey;
        public string Name => "TriggerModifierContinuous";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            ReadAndValidate(payload, out _);
        }

        public byte[] ExportState(FrameIndex frame)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(_service.NextProjectionId);
            var runtimes = _service.GetActive();
            writer.Write(runtimes.Count);
            foreach (var runtime in runtimes)
            {
                writer.Write(runtime.OwnerActorId);
                writer.Write(runtime.LegacySourceId);
                writer.Write(runtime.ProjectionId);
                writer.Write(runtime.IsPaused);
                writer.Write(runtime.Specs.Count);
                foreach (var spec in runtime.Specs)
                {
                    writer.Write(spec.TargetId);
                    writer.Write(spec.Op);
                    writer.Write(spec.Value);
                    writer.Write(spec.Priority);
                }
            }
            writer.Flush();
            return stream.ToArray();
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var runtimes = ReadAndValidate(payload, out var nextProjectionId);
            _service.Restore(runtimes, nextProjectionId);
        }

        private static List<TriggerModifierContinuous> ReadAndValidate(byte[] payload,
            out int nextProjectionId)
        {
            if (payload == null || payload.Length == 0) throw new InvalidDataException("Trigger modifier continuous payload is missing.");
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Version) throw new InvalidDataException("Unsupported trigger modifier continuous payload version.");
            nextProjectionId = reader.ReadInt32();
            if (nextProjectionId > 0)
                throw new InvalidDataException("Invalid trigger modifier projection allocator state.");
            var count = reader.ReadInt32();
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid trigger modifier continuous count.");
            var runtimes = new List<TriggerModifierContinuous>(count);
            var keys = new HashSet<(int OwnerId, int LegacySourceId)>();
            for (var i = 0; i < count; i++)
            {
                var ownerId = reader.ReadInt32();
                var legacySourceId = reader.ReadInt32();
                var projectionId = reader.ReadInt32();
                var paused = reader.ReadBoolean();
                var specCount = reader.ReadInt32();
                if (ownerId <= 0 || legacySourceId == 0 || projectionId >= 0 ||
                    specCount < 1 || specCount > 10000 || !keys.Add((ownerId, legacySourceId)))
                    throw new InvalidDataException("Invalid trigger modifier continuous entry.");
                var runtime = new TriggerModifierContinuous(ownerId, legacySourceId, projectionId) { WasPaused = paused };
                for (var j = 0; j < specCount; j++)
                {
                    var parameterId = reader.ReadInt32();
                    var op = reader.ReadInt32();
                    var value = reader.ReadSingle();
                    var priority = reader.ReadInt32();
                    if (!MobaSkillParamModifierKeys.IsTriggerWritable(parameterId) ||
                        !MobaSkillParamModifierKeys.SupportsOperation(parameterId, op) ||
                        float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidDataException("Invalid trigger modifier continuous spec.");
                    runtime.Specs.Add(new ContinuousModifierMO(new ContinuousModifierDTO
                    {
                        TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                        TargetId = parameterId,
                        Op = op,
                        Value = value,
                        Priority = priority,
                        EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.Realtime,
                    }));
                }
                runtimes.Add(runtime);
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Trigger modifier continuous payload contains trailing data.");
            return runtimes;
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }
    }
}

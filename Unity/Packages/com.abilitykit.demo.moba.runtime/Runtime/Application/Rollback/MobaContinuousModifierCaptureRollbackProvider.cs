using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaContinuousModifierCaptureRollbackProvider : IRollbackStateProvider,
        IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10031;
        private const int Version = 4;
        private readonly MobaContinuousManager _continuous;

        public MobaContinuousModifierCaptureRollbackProvider(MobaContinuousManager continuous)
        {
            _continuous = continuous ?? throw new ArgumentNullException(nameof(continuous));
        }

        public int Key => DefaultKey;
        public string Name => "ContinuousModifierCaptures";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            ReadAndValidate(payload, Collect(), out _);
        }

        public byte[] ExportState(FrameIndex frame)
        {
            var runtimes = Collect();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(_continuous.NextModifierSourceId);
            writer.Write(runtimes.Count);
            foreach (var pair in runtimes)
            {
                writer.Write(pair.Key.OwnerActorId);
                writer.Write(pair.Key.ModifierSourceId);
                writer.Write(pair.Key.TargetActorId);
                var captures = pair.Value.ModifierCaptures;
                var indices = new List<int>(captures.Keys);
                indices.Sort();
                writer.Write(indices.Count);
                foreach (var index in indices)
                {
                    writer.Write(index);
                    writer.Write(captures[index].Stack);
                    writer.Write(captures[index].Value);
                    writer.Write(captures[index].HasUnitValue);
                    if (captures[index].HasUnitValue) writer.Write(captures[index].UnitValue);
                }
            }
            writer.Flush();
            return stream.ToArray();
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var restored = ReadAndValidate(payload, Collect(), out var nextSourceId);
            _continuous.RestoreNextModifierSourceId(nextSourceId);
            foreach (var pair in restored) pair.Key.RestoreModifierCaptures(pair.Value);
            foreach (var pair in restored) _continuous.Reproject(pair.Key);
        }

        private List<KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>>
            ReadAndValidate(byte[] payload, SortedDictionary<Identity, MobaContinuousRuntimeBase> runtimes,
                out int nextSourceId)
        {
            if (payload == null || payload.Length == 0) throw new InvalidDataException("Continuous modifier capture payload is missing.");
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            var version = reader.ReadInt32();
            if (version != 2 && version != 3 && version != Version)
                throw new InvalidDataException("Unsupported continuous modifier capture payload version.");
            nextSourceId = version >= 4 ? reader.ReadInt32() : _continuous.NextModifierSourceId;
            _continuous.ValidateNextModifierSourceId(nextSourceId);
            var count = reader.ReadInt32();
            if (count < 0 || count > runtimes.Count) throw new InvalidOperationException("Continuous modifier capture membership changed since snapshot.");

            var restored = new List<KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>>(count);
            var restoredIds = new HashSet<Identity>();
            for (var entry = 0; entry < count; entry++)
            {
                var identity = new Identity(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                if (!restoredIds.Add(identity) || !runtimes.TryGetValue(identity, out var runtime))
                    throw new InvalidOperationException("Continuous modifier capture identity changed since snapshot.");
                var captureCount = reader.ReadInt32();
                if (captureCount < 0 || captureCount > 10000) throw new InvalidDataException("Invalid modifier capture count.");
                var captures = new Dictionary<int, MobaModifierCapturedValue>(captureCount);
                var specCount = (runtime.Config as IMobaContinuousModifierConfig)?.Modifiers?.Count ?? 0;
                for (var i = 0; i < captureCount; i++)
                {
                    var index = reader.ReadInt32();
                    var stack = reader.ReadInt32();
                    var value = reader.ReadSingle();
                    var hasUnitValue = version >= 3 && reader.ReadBoolean();
                    var unitValue = hasUnitValue ? reader.ReadSingle() : 0f;
                    if (index < 0 || index >= specCount || stack < 1 || float.IsNaN(value) || float.IsInfinity(value) ||
                        hasUnitValue && (float.IsNaN(unitValue) || float.IsInfinity(unitValue)) ||
                        !captures.TryAdd(index, new MobaModifierCapturedValue(stack, value,
                            hasUnitValue ? unitValue : (float?)null)))
                        throw new InvalidDataException("Invalid modifier capture entry.");
                }
                restored.Add(new KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>(runtime, captures));
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Continuous modifier capture payload contains trailing data.");
            foreach (var pair in runtimes)
                if (!restoredIds.Contains(pair.Key))
                    throw new InvalidOperationException("Continuous modifier capture membership changed since snapshot.");

            return restored;
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }

        private SortedDictionary<Identity, MobaContinuousRuntimeBase> Collect()
        {
            var result = new SortedDictionary<Identity, MobaContinuousRuntimeBase>();
            foreach (var continuous in _continuous.GetAllContinuous())
            {
                if (!(continuous is MobaContinuousRuntimeBase runtime) ||
                    runtime is TriggerModifierContinuous ||
                    !(runtime.Config is IMobaContinuousProjectionConfig projection) ||
                    !(runtime.Config is IMobaContinuousModifierConfig specs) ||
                    specs.Modifiers == null || specs.Modifiers.Count == 0 ||
                    projection.ModifierSourceId == 0) continue;
                var key = new Identity(projection.OwnerActorId, projection.ModifierSourceId, projection.TargetActorId);
                if (result.ContainsKey(key)) throw new InvalidOperationException("Continuous modifier capture identities are not unique.");
                result.Add(key, runtime);
            }
            return result;
        }

        private readonly struct Identity : IComparable<Identity>, IEquatable<Identity>
        {
            public Identity(int ownerActorId, int modifierSourceId, int targetActorId)
            {
                OwnerActorId = ownerActorId;
                ModifierSourceId = modifierSourceId;
                TargetActorId = targetActorId;
            }

            public int OwnerActorId { get; }
            public int ModifierSourceId { get; }
            public int TargetActorId { get; }
            public int CompareTo(Identity other)
            {
                var result = OwnerActorId.CompareTo(other.OwnerActorId);
                if (result != 0) return result;
                result = ModifierSourceId.CompareTo(other.ModifierSourceId);
                return result != 0 ? result : TargetActorId.CompareTo(other.TargetActorId);
            }
            public bool Equals(Identity other) => CompareTo(other) == 0;
            public override bool Equals(object obj) => obj is Identity other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = OwnerActorId;
                    hash = hash * 397 ^ ModifierSourceId;
                    return hash * 397 ^ TargetActorId;
                }
            }
        }
    }
}

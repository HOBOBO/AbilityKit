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
        private const int Version = 6;
        private const int LegacyVersion = 5;
        private readonly MobaContinuousManager _continuous;
        private readonly MobaTriggerIntervalContinuousService _triggerIntervals;

        public MobaContinuousModifierCaptureRollbackProvider(MobaContinuousManager continuous,
            MobaTriggerIntervalContinuousService triggerIntervals = null)
        {
            _continuous = continuous ?? throw new ArgumentNullException(nameof(continuous));
            _triggerIntervals = triggerIntervals;
        }

        public int Key => DefaultKey;
        public string Name => "ContinuousModifierCaptures";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            ReadAndValidate(payload, Collect(), out _, out _, preflight: true);
        }

        public byte[] ExportState(FrameIndex frame)
        {
            var runtimes = Collect();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(3);
            WriteSection(writer, "allocator", section =>
                section.Write(_continuous.NextModifierSourceId));
            WriteSection(writer, "intervals", section =>
            {
                var entries = _triggerIntervals?.CaptureRollbackEntries();
                section.Write(entries?.Count ?? 0);
                if (entries != null)
                    foreach (var entry in entries) WriteIntervalEntry(section, entry);
            });
            WriteSection(writer, "captures", section =>
            {
                section.Write(runtimes.Count);
                foreach (var pair in runtimes)
                {
                    section.Write(pair.Key.OwnerActorId);
                    section.Write(pair.Key.ModifierSourceId);
                    section.Write(pair.Key.TargetActorId);
                    var captures = pair.Value.ModifierCaptures;
                    var indices = new List<int>(captures.Keys);
                    indices.Sort();
                    section.Write(indices.Count);
                    foreach (var index in indices)
                    {
                        section.Write(index);
                        section.Write(captures[index].Stack);
                        section.Write(captures[index].Value);
                        section.Write(captures[index].HasUnitValue);
                        if (captures[index].HasUnitValue) section.Write(captures[index].UnitValue);
                    }
                }
            });
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteSection(BinaryWriter writer, string name, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var section = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                write(section);
            writer.Write(name);
            writer.Write((int)stream.Length);
            writer.Write(stream.ToArray());
        }

        private static byte[] ReadLegacyPayload(byte[] payload)
        {
            using var input = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(input);
            if (reader.ReadInt32() != Version) throw new InvalidDataException("Invalid continuous modifier version.");
            var count = reader.ReadInt32();
            if (count != 3) throw new InvalidDataException("Invalid continuous modifier section count.");
            var sections = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var length = reader.ReadInt32();
                if (length < 0 || length > input.Length - input.Position ||
                    !sections.TryAdd(name, reader.ReadBytes(length)))
                    throw new InvalidDataException("Invalid continuous modifier section.");
            }
            if (input.Position != input.Length || sections.Count != 3 ||
                !sections.TryGetValue("allocator", out var allocator) || allocator.Length != sizeof(int) ||
                !sections.TryGetValue("intervals", out var intervals) ||
                !sections.TryGetValue("captures", out var captures))
                throw new InvalidDataException("Continuous modifier sections are incomplete.");

            using var output = new MemoryStream();
            using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(LegacyVersion);
                writer.Write(allocator);
                writer.Write(intervals);
                writer.Write(captures);
            }
            return output.ToArray();
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var current = Collect();
            var restored = ReadAndValidate(payload, current, out var nextSourceId,
                out var intervals, preflight: true);
            if (_triggerIntervals != null && intervals != null)
            {
                _triggerIntervals.RestoreRollbackEntries(intervals);
                restored = ReadAndValidate(payload, Collect(), out nextSourceId,
                    out _, preflight: false);
            }
            _continuous.RestoreNextModifierSourceId(nextSourceId);
            foreach (var pair in restored) pair.Key.RestoreModifierCaptures(pair.Value);
            foreach (var pair in restored) _continuous.Reproject(pair.Key);
        }

        private List<KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>>
            ReadAndValidate(byte[] payload, SortedDictionary<Identity, MobaContinuousRuntimeBase> runtimes,
                out int nextSourceId, out List<MobaTriggerIntervalContinuousService.RollbackEntry> intervals,
                bool preflight)
        {
            if (payload == null || payload.Length == 0) throw new InvalidDataException("Continuous modifier capture payload is missing.");
            if (payload.Length >= sizeof(int) && BitConverter.ToInt32(payload, 0) == Version)
                payload = ReadLegacyPayload(payload);
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            var version = reader.ReadInt32();
            if (version != 2 && version != 3 && version != 4 && version != LegacyVersion)
                throw new InvalidDataException("Unsupported continuous modifier capture payload version.");
            nextSourceId = version >= 4 ? reader.ReadInt32() : _continuous.NextModifierSourceId;
            intervals = null;
            var intervalIds = new Dictionary<Identity, int>();
            if (version >= 5)
            {
                var intervalCount = reader.ReadInt32();
                if (intervalCount < 0 || intervalCount > 100000)
                    throw new InvalidDataException("Invalid trigger interval rollback count.");
                intervals = new List<MobaTriggerIntervalContinuousService.RollbackEntry>(intervalCount);
                for (var i = 0; i < intervalCount; i++) intervals.Add(ReadIntervalEntry(reader));
                if (_triggerIntervals == null && intervalCount != 0)
                    throw new InvalidOperationException("Trigger interval rollback service is unavailable.");
                _triggerIntervals?.ValidateRollbackEntries(intervals);
                foreach (var entry in intervals)
                    if (entry.ModifierSourceId > 0)
                        intervalIds.Add(new Identity(entry.TargetActorId, entry.ModifierSourceId,
                            entry.TargetActorId), _triggerIntervals.GetRollbackModifierCount(entry.ProcessId));
                if (_triggerIntervals == null) intervals = null;
            }
            if (nextSourceId <= 0) throw new InvalidDataException("Invalid modifier source allocator state.");
            if (!preflight || intervals == null) _continuous.ValidateNextModifierSourceId(nextSourceId);
            var count = reader.ReadInt32();
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid continuous modifier capture count.");

            var restored = new List<KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>>(count);
            var restoredIds = new HashSet<Identity>();
            for (var entry = 0; entry < count; entry++)
            {
                var identity = new Identity(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                if (!restoredIds.Add(identity))
                    throw new InvalidOperationException("Continuous modifier capture identity changed since snapshot.");
                runtimes.TryGetValue(identity, out var runtime);
                var plannedInterval = intervalIds.TryGetValue(identity, out var intervalSpecCount);
                if (preflight && intervals != null && runtime != null)
                {
                    if (runtime is MobaTriggerIntervalContinuousRuntime trigger &&
                        _triggerIntervals.OwnsRuntime(trigger)) runtime = null;
                    else if (plannedInterval)
                        throw new InvalidOperationException("Trigger interval source identity collides with another continuous.");
                }
                if (runtime == null && (!preflight || !plannedInterval))
                    throw new InvalidOperationException("Continuous modifier capture identity changed since snapshot.");
                if (plannedInterval && identity.ModifierSourceId >= nextSourceId)
                    throw new InvalidDataException("Modifier source allocator state precedes a captured source.");
                var captureCount = reader.ReadInt32();
                if (captureCount < 0 || captureCount > 10000) throw new InvalidDataException("Invalid modifier capture count.");
                var captures = new Dictionary<int, MobaModifierCapturedValue>(captureCount);
                var specCount = runtime != null
                    ? (runtime.Config as IMobaContinuousModifierConfig)?.Modifiers?.Count ?? 0
                    : intervalSpecCount;
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
                if (runtime != null)
                    restored.Add(new KeyValuePair<MobaContinuousRuntimeBase, Dictionary<int, MobaModifierCapturedValue>>(runtime, captures));
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Continuous modifier capture payload contains trailing data.");
            foreach (var pair in runtimes)
                if (!restoredIds.Contains(pair.Key) &&
                    !(preflight && intervals != null &&
                      pair.Value is MobaTriggerIntervalContinuousRuntime trigger &&
                      _triggerIntervals.OwnsRuntime(trigger)))
                    throw new InvalidOperationException("Continuous modifier capture membership changed since snapshot.");
            if (preflight && intervals != null)
            {
                foreach (var pair in intervalIds)
                    if (!restoredIds.Contains(pair.Key))
                        throw new InvalidOperationException("Trigger interval modifier capture is missing.");
                foreach (var pair in runtimes)
                    if (!(pair.Value is MobaTriggerIntervalContinuousRuntime) &&
                        pair.Value.AllocatedModifierSourceId > 0 &&
                        pair.Value.AllocatedModifierSourceId >= nextSourceId)
                        throw new InvalidDataException("Modifier source allocator state precedes an active source.");
            }

            return restored;
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }

        private static void WriteIntervalEntry(BinaryWriter writer,
            MobaTriggerIntervalContinuousService.RollbackEntry entry)
        {
            writer.Write(entry.ProcessId);
            writer.Write(entry.SourceActorId);
            writer.Write(entry.TargetActorId);
            writer.Write(entry.OwnerContextId);
            writer.Write(entry.ModifierSourceId);
            writer.Write(entry.ElapsedRaw);
            writer.Write(entry.IntervalRemainingRaw);
            writer.Write(entry.Paused);
            var source = entry.Source;
            writer.Write((int)source.ResolveKind);
            writer.Write((int)source.Boundary);
            writer.Write((int)source.ContextKind);
            writer.Write((int)source.ExecutionKind);
            writer.Write(source.SourceActorId);
            writer.Write(source.TargetActorId);
            writer.Write(source.SourceContextId);
            writer.Write(source.ParentContextId);
            writer.Write(source.RootContextId);
            writer.Write(source.OwnerContextId);
            writer.Write(source.ConfigId);
            writer.Write(source.TriggerId);
            writer.Write(source.Frame);
            writer.Write(source.RuntimeKind ?? string.Empty);
            writer.Write(source.RuntimeConfigId);
            writer.Write(source.HasLiveRuntime);
            writer.Write(source.SkillRuntimeHandle.RuntimeId);
            writer.Write(source.SkillRuntimeHandle.Generation);
            writer.Write(source.SkillRuntimeHandle.RootContextId);
        }

        private static MobaTriggerIntervalContinuousService.RollbackEntry ReadIntervalEntry(BinaryReader reader)
        {
            var processId = reader.ReadInt32();
            var sourceActorId = reader.ReadInt32();
            var targetActorId = reader.ReadInt32();
            var ownerContextId = reader.ReadInt64();
            var modifierSourceId = reader.ReadInt32();
            var elapsedRaw = reader.ReadInt64();
            var intervalRemainingRaw = reader.ReadInt64();
            var paused = reader.ReadBoolean();
            var resolveKind = (MobaContextSourceResolveKind)reader.ReadInt32();
            var boundary = (MobaContextSourceBoundary)reader.ReadInt32();
            var contextKind = (EffectContextKind)reader.ReadInt32();
            var executionKind = (MobaExecutionKind)reader.ReadInt32();
            var source = new MobaContextSourceView(resolveKind, boundary, contextKind,
                executionKind, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(),
                reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadString(), reader.ReadInt32(),
                reader.ReadBoolean(), new MobaSkillCastRuntimeHandle(reader.ReadInt64(),
                    reader.ReadInt32(), reader.ReadInt64()));
            return new MobaTriggerIntervalContinuousService.RollbackEntry(processId,
                sourceActorId, targetActorId, ownerContextId, modifierSourceId, elapsedRaw,
                intervalRemainingRaw, paused, source);
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

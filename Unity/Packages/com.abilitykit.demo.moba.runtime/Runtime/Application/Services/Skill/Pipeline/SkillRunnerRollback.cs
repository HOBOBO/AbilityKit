using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Continuous;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.ECS;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillRunnerRollbackEntry
    {
        public int ActorId;
        public int SkillId;
        public int SkillSlot;
        public int SkillLevel;
        public int BaseSkillId;
        public int Sequence;
        public long RuntimeId;
        public int RuntimeGeneration;
        public long RootContextId;
        public long SourceContextId;
        public long DiagnosticCommandId;
        public int CastFlowId;
        public string TriggerFailReason;
        public int Stage;
        public int StartFrame;
        public long PhaseContextId;
        public SkillCastPolicy Policy;
        public SkillPipelineContextRollbackState Context;
        public byte[] Run;
    }

    internal sealed class SkillRunnerRollbackSnapshot
    {
        public KeyValuePair<int, int>[] Sequences = Array.Empty<KeyValuePair<int, int>>();
        public SkillRunnerRollbackEntry[] Entries = Array.Empty<SkillRunnerRollbackEntry>();
    }

    internal static class SkillRunnerRollbackCodec
    {
        private const int Version = 1;
        private const int MaxEntries = 1024;
        private const int MaxBytes = 8 * 1024 * 1024;

        public static byte[] Serialize(SkillRunnerRollbackSnapshot snapshot)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Version);
            WriteCount(writer, snapshot.Sequences.Length);
            foreach (var sequence in snapshot.Sequences)
            {
                writer.Write(sequence.Key);
                writer.Write(sequence.Value);
            }
            WriteCount(writer, snapshot.Entries.Length);
            foreach (var entry in snapshot.Entries) WriteEntry(writer, entry);
            writer.Flush();
            if (stream.Length > MaxBytes) throw new InvalidDataException("Skill runner rollback payload is too large.");
            return stream.ToArray();
        }

        public static SkillRunnerRollbackSnapshot Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 12 || payload.Length > MaxBytes)
                throw new InvalidDataException("Invalid skill runner rollback payload length.");
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != Version)
                throw new InvalidDataException("Unsupported skill runner rollback version.");
            var snapshot = new SkillRunnerRollbackSnapshot();
            var sequenceCount = ReadCount(reader);
            snapshot.Sequences = new KeyValuePair<int, int>[sequenceCount];
            var previousActor = 0;
            for (var i = 0; i < sequenceCount; i++)
            {
                var actor = reader.ReadInt32();
                var sequence = reader.ReadInt32();
                if (actor <= previousActor || sequence <= 0)
                    throw new InvalidDataException("Invalid skill cast sequence state.");
                snapshot.Sequences[i] = new KeyValuePair<int, int>(actor, sequence);
                previousActor = actor;
            }
            var entryCount = ReadCount(reader);
            snapshot.Entries = new SkillRunnerRollbackEntry[entryCount];
            var identities = new HashSet<long>();
            for (var i = 0; i < entryCount; i++)
            {
                var entry = ReadEntry(reader);
                if (entry.ActorId <= 0 || entry.SkillId <= 0 || entry.SkillLevel <= 0 ||
                    entry.SkillSlot < 0 || entry.Sequence <= 0 || entry.RuntimeId <= 0 ||
                    entry.RuntimeGeneration <= 0 || entry.RootContextId <= 0 ||
                    entry.SourceContextId != entry.RootContextId || entry.StartFrame < 0 ||
                    entry.Stage is < 0 or > 1 || entry.Run.Length == 0 ||
                    !identities.Add(entry.RuntimeId))
                    throw new InvalidDataException("Invalid skill runner rollback entry.");
                snapshot.Entries[i] = entry;
            }
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Trailing skill runner rollback bytes.");
            return snapshot;
        }

        private static void WriteEntry(BinaryWriter writer, SkillRunnerRollbackEntry entry)
        {
            writer.Write(entry.ActorId);
            writer.Write(entry.SkillId);
            writer.Write(entry.SkillSlot);
            writer.Write(entry.SkillLevel);
            writer.Write(entry.BaseSkillId);
            writer.Write(entry.Sequence);
            writer.Write(entry.RuntimeId);
            writer.Write(entry.RuntimeGeneration);
            writer.Write(entry.RootContextId);
            writer.Write(entry.SourceContextId);
            writer.Write(entry.DiagnosticCommandId);
            writer.Write(entry.CastFlowId);
            writer.Write(entry.TriggerFailReason ?? string.Empty);
            writer.Write(entry.Stage);
            writer.Write(entry.StartFrame);
            writer.Write(entry.PhaseContextId);
            var policy = entry.Policy;
            writer.Write(policy.AllowParallel);
            writer.Write(policy.InterruptRunning);
            writer.Write(policy.ConflictGroup);
            writer.Write(policy.InterruptPriority);
            writer.Write(policy.Uninterruptible);
            writer.Write((int)policy.TargetLostPolicy);
            WriteContext(writer, entry.Context);
            writer.Write(entry.Run.Length);
            writer.Write(entry.Run);
        }

        private static SkillRunnerRollbackEntry ReadEntry(BinaryReader reader)
        {
            var entry = new SkillRunnerRollbackEntry
            {
                ActorId = reader.ReadInt32(), SkillId = reader.ReadInt32(),
                SkillSlot = reader.ReadInt32(), SkillLevel = reader.ReadInt32(),
                BaseSkillId = reader.ReadInt32(), Sequence = reader.ReadInt32(),
                RuntimeId = reader.ReadInt64(), RuntimeGeneration = reader.ReadInt32(),
                RootContextId = reader.ReadInt64(), SourceContextId = reader.ReadInt64(),
                DiagnosticCommandId = reader.ReadInt64(), CastFlowId = reader.ReadInt32(),
                TriggerFailReason = reader.ReadString(), Stage = reader.ReadInt32(),
                StartFrame = reader.ReadInt32(), PhaseContextId = reader.ReadInt64()
            };
            var parallel = reader.ReadBoolean();
            var interrupt = reader.ReadBoolean();
            var conflict = reader.ReadInt32();
            var priority = reader.ReadInt32();
            var uninterruptible = reader.ReadBoolean();
            var targetLost = reader.ReadInt32();
            if (targetLost < 0 || targetLost > (int)SkillTargetLostPolicy.Reacquire)
                throw new InvalidDataException("Invalid skill target-loss policy.");
            entry.Policy = new SkillCastPolicy(parallel, interrupt, conflict, priority,
                uninterruptible, (SkillTargetLostPolicy)targetLost);
            entry.Context = ReadContext(reader);
            var length = reader.ReadInt32();
            if (length <= 0 || length > 4 * 1024 * 1024 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid skill pipeline run length.");
            entry.Run = reader.ReadBytes(length);
            PipelineRollbackStateCodec.Deserialize(entry.Run);
            return entry;
        }

        private static void WriteContext(BinaryWriter writer, in SkillPipelineContextRollbackState state)
        {
            writer.Write(state.ElapsedRaw);
            writer.Write(state.Frame);
            writer.Write(state.TargetActorId);
            WriteVec(writer, state.AimPos);
            WriteVec(writer, state.AimDir);
            writer.Write(state.CastSequence);
            writer.Write(state.TimelineNextEventIndex);
            writer.Write(state.InputReleased);
            writer.Write(state.SkillCooldownMs);
            writer.Write(state.FailReason ?? string.Empty);
            writer.Write(state.SourceContextId);
            writer.Write(state.DiagnosticCommandId);
            writer.Write(state.CastFlowId);
            var config = state.ResolvedConfiguration;
            writer.Write(config.SkillId);
            writer.Write(config.SkillLevel);
            writer.Write((int)config.ResourceType);
            writer.Write(config.ResourceCost);
            writer.Write(config.CooldownMs);
            writer.Write(config.HasLevelConfiguration);
            writer.Write(state.IsAborted);
            writer.Write(state.StartTime);
            if (state.SharedData.Length > 256)
                throw new InvalidDataException("Too many skill shared-data entries.");
            writer.Write(state.SharedData.Length);
            foreach (var item in state.SharedData)
            {
                writer.Write(item.Key);
                writer.Write(item.Kind);
                switch (item.Kind)
                {
                    case SkillSharedDataRollbackEntry.Null: break;
                    case SkillSharedDataRollbackEntry.Integer:
                    case SkillSharedDataRollbackEntry.Boolean: writer.Write(item.IntegerValue); break;
                    case SkillSharedDataRollbackEntry.Long: writer.Write(item.LongValue); break;
                    case SkillSharedDataRollbackEntry.Float:
                    case SkillSharedDataRollbackEntry.Double: writer.Write(item.Number); break;
                    case SkillSharedDataRollbackEntry.String: writer.Write(item.Text); break;
                    case SkillSharedDataRollbackEntry.Vector: WriteVec(writer, item.VectorValue); break;
                    case SkillSharedDataRollbackEntry.RuntimeHandle:
                        writer.Write(item.Handle.RuntimeId);
                        writer.Write(item.Handle.Generation);
                        writer.Write(item.Handle.RootContextId);
                        break;
                    default: throw new InvalidDataException("Invalid skill shared-data kind.");
                }
            }
        }

        private static SkillPipelineContextRollbackState ReadContext(BinaryReader reader)
        {
            var elapsed = reader.ReadInt64();
            var frame = reader.ReadInt32();
            var target = reader.ReadInt32();
            var aimPos = ReadVec(reader);
            var aimDir = ReadVec(reader);
            var sequence = reader.ReadInt32();
            var nextEvent = reader.ReadInt32();
            var released = reader.ReadBoolean();
            var cooldown = reader.ReadInt32();
            var failReason = reader.ReadString();
            var sourceId = reader.ReadInt64();
            var diagnosticId = reader.ReadInt64();
            var flowId = reader.ReadInt32();
            var skillId = reader.ReadInt32();
            var skillLevel = reader.ReadInt32();
            var resourceType = reader.ReadInt32();
            var cost = reader.ReadInt32();
            var configCooldown = reader.ReadInt32();
            var hasLevel = reader.ReadBoolean();
            var aborted = reader.ReadBoolean();
            var startTime = reader.ReadSingle();
            var sharedCount = reader.ReadInt32();
            if (sharedCount < 0 || sharedCount > 256)
                throw new InvalidDataException("Invalid skill shared-data count.");
            var shared = new SkillSharedDataRollbackEntry[sharedCount];
            string previousKey = null;
            for (var i = 0; i < sharedCount; i++)
            {
                var key = reader.ReadString();
                var kind = reader.ReadByte();
                if (string.IsNullOrEmpty(key) || key.Length > 256 ||
                    previousKey != null && string.CompareOrdinal(previousKey, key) >= 0)
                    throw new InvalidDataException("Invalid skill shared-data key order.");
                previousKey = key;
                var integer = 0;
                long longValue = 0;
                double number = 0;
                string text = null;
                Vec3 vector = default;
                MobaSkillCastRuntimeHandle handle = default;
                switch (kind)
                {
                    case SkillSharedDataRollbackEntry.Null: break;
                    case SkillSharedDataRollbackEntry.Integer:
                    case SkillSharedDataRollbackEntry.Boolean: integer = reader.ReadInt32(); break;
                    case SkillSharedDataRollbackEntry.Long: longValue = reader.ReadInt64(); break;
                    case SkillSharedDataRollbackEntry.Float:
                    case SkillSharedDataRollbackEntry.Double: number = reader.ReadDouble(); break;
                    case SkillSharedDataRollbackEntry.String: text = reader.ReadString(); break;
                    case SkillSharedDataRollbackEntry.Vector: vector = ReadVec(reader); break;
                    case SkillSharedDataRollbackEntry.RuntimeHandle:
                        handle = new MobaSkillCastRuntimeHandle(reader.ReadInt64(),
                            reader.ReadInt32(), reader.ReadInt64());
                        break;
                    default: throw new InvalidDataException("Invalid skill shared-data kind.");
                }
                if (double.IsNaN(number) || double.IsInfinity(number) ||
                    kind == SkillSharedDataRollbackEntry.Boolean && integer is < 0 or > 1)
                    throw new InvalidDataException("Invalid skill shared-data value.");
                shared[i] = new SkillSharedDataRollbackEntry(key, kind, integer, longValue,
                    number, text, vector, handle);
            }
            if (elapsed < 0 || frame < 0 || target < 0 || sequence < 0 || nextEvent < 0 ||
                cooldown < 0 || flowId < 0 || skillId < 0 || skillLevel < 0 || cost < 0 ||
                configCooldown < 0 || float.IsNaN(startTime) || float.IsInfinity(startTime))
                throw new InvalidDataException("Invalid skill pipeline context state.");
            var config = new ResolvedSkillCastConfiguration(skillId, skillLevel,
                (ResourceType)resourceType, cost, configCooldown, hasLevel);
            return new SkillPipelineContextRollbackState(elapsed, frame, target, aimPos, aimDir,
                sequence, nextEvent, released, cooldown, failReason, sourceId, diagnosticId,
                flowId, config, aborted, startTime, shared);
        }

        private static void WriteVec(BinaryWriter writer, in Vec3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        private static Vec3 ReadVec(BinaryReader reader)
        {
            var value = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z) ||
                float.IsInfinity(value.X) || float.IsInfinity(value.Y) || float.IsInfinity(value.Z))
                throw new InvalidDataException("Invalid skill runner vector.");
            return value;
        }

        private static int ReadCount(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries) throw new InvalidDataException("Invalid skill runner entry count.");
            return count;
        }

        private static void WriteCount(BinaryWriter writer, int count)
        {
            if (count < 0 || count > MaxEntries) throw new InvalidDataException("Too many skill runner entries.");
            writer.Write(count);
        }
    }

    public sealed partial class SkillPipelineRunner
    {
        internal void CaptureRollbackEntries(List<SkillRunnerRollbackEntry> destination)
        {
            foreach (var entry in _running)
            {
                if (entry.Run == null || entry.Context == null || entry.TriggerContext == null ||
                    entry.Run.State != EAbilityPipelineState.Executing ||
                    (entry.Continuous != null && entry.Continuous.IsTerminated))
                    throw new InvalidOperationException("Skill runner contains an invalid active rollback entry.");
                var trigger = entry.TriggerContext;
                destination.Add(new SkillRunnerRollbackEntry
                {
                    ActorId = _actorId, SkillId = trigger.SkillId, SkillSlot = trigger.SkillSlot,
                    SkillLevel = trigger.SkillLevel, BaseSkillId = trigger.BaseSkillId,
                    Sequence = trigger.Sequence, RuntimeId = trigger.RuntimeHandle.RuntimeId,
                    RuntimeGeneration = trigger.RuntimeHandle.Generation,
                    RootContextId = trigger.RuntimeHandle.RootContextId,
                    SourceContextId = trigger.SourceContextId,
                    DiagnosticCommandId = trigger.DiagnosticCommandId,
                    CastFlowId = trigger.CastFlowId, TriggerFailReason = trigger.FailReason,
                    Stage = (int)entry.Stage, StartFrame = entry.StartFrame,
                    PhaseContextId = entry.PhaseContextId, Policy = entry.Policy,
                    Context = entry.Context.CaptureRollbackState(),
                    Run = PipelineRollbackStateCodec.Serialize(SkillPipelineRollback.Capture(entry.Run,
                        entry.Stage == EntryStage.PreCast ? entry.PreCastConfig : entry.CastConfig))
                });
            }
        }

        internal void DetachForRollback()
        {
            foreach (var entry in _running)
            {
                SkillPipelineRollback.Detach(entry.Run);
                if (entry.Continuous != null && entry.Request.WorldServices != null &&
                    entry.Request.WorldServices.TryResolve<IContinuousManager>(out var continuous))
                    continuous.Unregister(entry.Continuous, ContinuousEndReason.CleanedUp);
                entry.Context?.DiscardCleanupsForRollback();
            }
            _running.Clear();
            _ended.Clear();
        }

        internal void RestoreRollbackEntry(SkillRunnerRollbackEntry state,
            IAbilityPipelineConfig preConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> prePhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance, in SkillCastRequest request, SkillCastContext trigger)
        {
            var entry = new Entry(preConfig, prePhases, castConfig, castPhases,
                abilityInstance, request, trigger, state.Policy)
            {
                Stage = (EntryStage)state.Stage,
                StartFrame = state.StartFrame,
                PhaseContextId = state.PhaseContextId
            };
            var config = entry.Stage == EntryStage.PreCast ? preConfig : castConfig;
            var phases = entry.Stage == EntryStage.PreCast ? prePhases : castPhases;
            if (config == null || phases == null || phases.Count == 0)
                throw new InvalidOperationException("Saved skill stage has no pipeline definition.");
            entry.Context = new SkillPipelineContext();
            entry.Context.Initialize(abilityInstance, in request, trigger);
            entry.Context.RestoreRollbackState(in state.Context);
            if (!TryBindPipelineContinuous(ref entry, config, out var bindError))
                throw new InvalidOperationException(bindError);
            try
            {
                entry.Pipeline = CreatePipeline();
                foreach (var phase in phases) entry.Pipeline.AddPhase(phase);
                entry.Run = SkillPipelineRollback.Restore(entry.Pipeline, config, entry.Context,
                    PipelineRollbackStateCodec.Deserialize(state.Run));
                _running.Add(entry);
            }
            catch
            {
                if (entry.Continuous != null && request.WorldServices != null &&
                    request.WorldServices.TryResolve<IContinuousManager>(out var continuous))
                    continuous.Unregister(entry.Continuous, ContinuousEndReason.CleanedUp);
                entry.Context.DiscardCleanupsForRollback();
                throw;
            }
        }
    }

    public sealed partial class SkillRunnerRegistry
    {
        internal SkillRunnerRollbackEntry[] CaptureRollbackEntries()
        {
            var values = new List<SkillRunnerRollbackEntry>();
            var actors = new List<int>(_runners.Keys);
            actors.Sort();
            foreach (var actor in actors) _runners[actor].CaptureRollbackEntries(values);
            return values.ToArray();
        }

        internal void DetachForRollback()
        {
            foreach (var runner in _runners.Values) runner.DetachForRollback();
            _runners.Clear();
        }
    }

    public sealed partial class SkillCastCoordinator
    {
        internal byte[] ExportRunnerRollbackState()
        {
            return SkillRunnerRollbackCodec.Serialize(new SkillRunnerRollbackSnapshot
            {
                Sequences = _preparation.CaptureRollbackSequences(),
                Entries = _runnerRegistry.CaptureRollbackEntries()
            });
        }

        internal void ValidateRunnerRollbackState(byte[] payload)
        {
            var snapshot = SkillRunnerRollbackCodec.Deserialize(payload);
            foreach (var entry in snapshot.Entries)
            {
                if (!_library.TryGet(entry.SkillId, out var preConfig, out var prePhases,
                        out var castConfig, out var castPhases))
                    throw new InvalidOperationException($"Skill rollback definition {entry.SkillId} is missing.");
                var config = entry.Stage == 0 ? preConfig : castConfig;
                var phases = entry.Stage == 0 ? prePhases : castPhases;
                var run = PipelineRollbackStateCodec.Deserialize(entry.Run);
                if (config == null || phases == null || phases.Count == 0 ||
                    entry.Context.SourceContextId != entry.SourceContextId ||
                    entry.Context.ResolvedConfiguration.SkillId != entry.SkillId)
                    throw new InvalidOperationException("Skill rollback definition or context does not match its saved run.");
                SkillPipelineRollback.ValidateDefinition(config, phases, run);
            }
        }

        internal void ImportRunnerRollbackState(byte[] payload)
        {
            ValidateRunnerRollbackState(payload);
            var snapshot = SkillRunnerRollbackCodec.Deserialize(payload);
            foreach (var state in snapshot.Entries)
            {
                if (!_units.TryResolve(new EcsEntityId(state.ActorId), out var caster) || caster == null)
                    throw new InvalidOperationException($"Skill rollback caster {state.ActorId} is missing.");
                var targetActorId = state.Context.TargetActorId;
                if (targetActorId > 0 &&
                    !_units.TryResolve(new EcsEntityId(targetActorId), out var target))
                    throw new InvalidOperationException($"Skill rollback target {targetActorId} is missing.");
                var handle = new MobaSkillCastRuntimeHandle(state.RuntimeId,
                    state.RuntimeGeneration, state.RootContextId);
                if (!_services.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) ||
                    runtimes == null || !runtimes.TryGet(in handle, out _))
                    throw new InvalidOperationException($"Skill rollback runtime {state.RuntimeId} was not restored.");
                if (state.PhaseContextId != 0 &&
                    (!_services.TryResolve<MobaExecutionContextRegistry>(out var contexts) ||
                     contexts == null || !contexts.TryGet(state.PhaseContextId, out var phase) ||
                     phase.IsEnded || phase.Kind != MobaExecutionKind.SkillPhase ||
                     phase.RootContextId != state.RootContextId))
                    throw new InvalidOperationException($"Skill rollback phase context {state.PhaseContextId} was not restored.");
            }
            _runnerRegistry.DetachForRollback();
            _preparation.RestoreRollbackSequences(snapshot.Sequences);
            foreach (var state in snapshot.Entries)
            {
                if (!_library.TryGet(state.SkillId, out var preConfig, out var prePhases,
                        out var castConfig, out var castPhases))
                    throw new InvalidOperationException($"Skill rollback definition {state.SkillId} is missing.");
                if (!_units.TryResolve(new EcsEntityId(state.ActorId), out var caster) || caster == null)
                    throw new InvalidOperationException($"Skill rollback caster {state.ActorId} is missing.");
                IUnitFacade target = null;
                var contextState = state.Context;
                if (contextState.TargetActorId > 0 &&
                    !_units.TryResolve(new EcsEntityId(contextState.TargetActorId), out target))
                    throw new InvalidOperationException($"Skill rollback target {contextState.TargetActorId} is missing.");
                var aimPos = contextState.AimPos;
                var aimDir = contextState.AimDir;
                var request = new SkillCastRequest(state.SkillId, state.SkillSlot, state.ActorId,
                    contextState.TargetActorId, in aimPos, in aimDir, _services, _eventBus, caster, target);
                var trigger = SkillCastContext.FromRequest(in request, state.SkillLevel);
                trigger.BaseSkillId = state.BaseSkillId;
                trigger.Sequence = state.Sequence;
                trigger.CastFlowId = state.CastFlowId;
                trigger.ResolvedConfiguration = contextState.ResolvedConfiguration;
                trigger.RuntimeHandle = new MobaSkillCastRuntimeHandle(state.RuntimeId,
                    state.RuntimeGeneration, state.RootContextId);
                if (!_services.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) ||
                    runtimes == null || !runtimes.TryGet(in trigger.RuntimeHandle, out _))
                    throw new InvalidOperationException($"Skill rollback runtime {state.RuntimeId} was not restored.");
                if (state.PhaseContextId != 0)
                {
                    if (!_services.TryResolve<MobaExecutionContextRegistry>(out var contexts) ||
                        contexts == null || !contexts.TryGet(state.PhaseContextId, out var phase) ||
                        phase.IsEnded || phase.Kind != MobaExecutionKind.SkillPhase ||
                        phase.RootContextId != state.RootContextId)
                        throw new InvalidOperationException($"Skill rollback phase context {state.PhaseContextId} was not restored.");
                }
                trigger.RuntimeId = state.RuntimeId;
                trigger.SourceContextId = state.SourceContextId;
                trigger.DiagnosticCommandId = state.DiagnosticCommandId;
                trigger.FailReason = state.TriggerFailReason;
                _runnerRegistry.GetOrCreate(state.ActorId).RestoreRollbackEntry(state,
                    preConfig, prePhases, castConfig, castPhases, this, in request, trigger);
            }
        }

    }
}

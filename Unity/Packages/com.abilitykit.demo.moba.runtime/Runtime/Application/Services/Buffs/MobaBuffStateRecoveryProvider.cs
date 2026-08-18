using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Lifecycle;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Services.Buffs
{
    [WorldService(typeof(MobaBuffStateRecoveryProvider))]
    public sealed class MobaBuffStateRecoveryProvider : IMobaStagedStateRecoveryProvider
    {
        public const int DefaultKey = 10030;
        public const int CurrentPayloadVersion = 2;

        private readonly MobaActorRegistry _actors;
        private readonly MobaRuntimeContextService _runtimeContexts;
        private readonly MobaSkillCastRuntimeService _skillRuntimes;
        private readonly BuffRuntimeBindingCoordinator _runtimeBindings;
        [WorldInject(required: false)] private MobaConfigDatabase _configs = null;
        [WorldInject(required: false)] private IMobaContinuousTagTemplateRegistry _tagTemplates = null;
        [WorldInject(required: false)] private IMobaEffectiveTagQueryService _tags = null;
        [WorldInject(required: false)] private IContinuousManager _continuous = null;

        public MobaBuffStateRecoveryProvider(
            MobaActorRegistry actors,
            MobaRuntimeContextService runtimeContexts,
            MobaSkillCastRuntimeService skillRuntimes)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _runtimeContexts = runtimeContexts ?? throw new ArgumentNullException(nameof(runtimeContexts));
            _skillRuntimes = skillRuntimes ?? throw new ArgumentNullException(nameof(skillRuntimes));
            _runtimeBindings = new BuffRuntimeBindingCoordinator(null, null, _skillRuntimes);
        }

        public int Key => DefaultKey;

        public string Name => "Buff";

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = new List<MobaBuffStateRecoveryEntry>(16);
            foreach (var kv in _actors.Entries)
            {
                var actorId = kv.Key;
                var actor = kv.Value;
                if (actor == null || !actor.hasBuffs || actor.buffs.Active == null) continue;

                var active = actor.buffs.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    var runtime = active[i];
                    if (runtime == null || runtime.BuffId <= 0) continue;
                    entries.Add(MobaBuffStateRecoveryEntry.FromRuntime(actorId, runtime));
                }
            }

            entries.Sort(CompareEntries);
            return MemoryPackSerializer.Serialize(new MobaBuffStateRecoveryPayload(CurrentPayloadVersion, entries.Count == 0 ? Array.Empty<MobaBuffStateRecoveryEntry>() : entries.ToArray()));
        }

        public void PrepareRestore(FrameIndex frame, byte[] payload)
        {
            ParseAndValidate(payload);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var incoming = ParseAndValidate(payload);
            var rollbackPayload = ExportState(frame);

            try
            {
                ApplyPrepared(frame, incoming);
            }
            catch (Exception restoreFailure)
            {
                try
                {
                    ApplyPrepared(frame, ParseAndValidate(rollbackPayload));
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException("Buff state restore failed and rollback could not restore the previous state.", restoreFailure, rollbackFailure);
                }

                throw new InvalidOperationException("Buff state restore failed; the previous Buff state was restored.", restoreFailure);
            }
        }

        public void ValidateRestoredState(FrameIndex frame, byte[] payload)
        {
            var expected = ParseAndValidate(payload);
            var actual = ParseAndValidate(ExportState(frame));
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException($"Buff state validation failed. expectedCount={expected.Length} actualCount={actual.Length}.");
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (!EntriesEqual(in expected[i], in actual[i]))
                {
                    throw new InvalidOperationException($"Buff state validation failed at index {i}. target={expected[i].TargetActorId} buffId={expected[i].BuffId} sourceContextId={expected[i].SourceContextId}.");
                }
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = MemoryPackSerializer.Deserialize<MobaBuffStateRecoveryPayload>(ExportState(frame));
            var entries = payload.Entries ?? Array.Empty<MobaBuffStateRecoveryEntry>();

            hash.AddInt(Key);
            hash.AddInt(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                AddEntryHash(entries[i], ref hash);
            }
        }

        private MobaBuffStateRecoveryEntry[] ParseAndValidate(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return Array.Empty<MobaBuffStateRecoveryEntry>();

            MobaBuffStateRecoveryPayload snapshot;
            try
            {
                snapshot = MemoryPackSerializer.Deserialize<MobaBuffStateRecoveryPayload>(payload);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Buff state payload could not be deserialized.", ex);
            }

            if (snapshot.Version != CurrentPayloadVersion)
            {
                throw new InvalidOperationException($"Unsupported Buff state payload version. expected={CurrentPayloadVersion} actual={snapshot.Version}.");
            }

            var entries = snapshot.Entries ?? Array.Empty<MobaBuffStateRecoveryEntry>();
            Array.Sort(entries, CompareEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.TargetActorId <= 0 || entry.BuffId <= 0 || entry.SourceContextId == 0L)
                {
                    throw new InvalidOperationException($"Invalid Buff state identity. target={entry.TargetActorId} buffId={entry.BuffId} sourceContextId={entry.SourceContextId}.");
                }

                if (i > 0 && SameIdentity(in entries[i - 1], in entry))
                {
                    throw new InvalidOperationException($"Duplicate Buff state identity. target={entry.TargetActorId} buffId={entry.BuffId} source={entry.SourceActorId} sourceContextId={entry.SourceContextId}.");
                }

                if (!_actors.TryGet(entry.TargetActorId, out var actor) || actor == null)
                {
                    throw new InvalidOperationException($"Buff state target actor not found. target={entry.TargetActorId} buffId={entry.BuffId}.");
                }

                var skillHandle = entry.SkillRuntimeHandle;
                if (skillHandle.IsValid && !_skillRuntimes.TryGet(in skillHandle, out _))
                {
                    throw new InvalidOperationException($"Buff state parent runtime not found. target={entry.TargetActorId} buffId={entry.BuffId} runtime={skillHandle}.");
                }

                if (entry.HasContinuous)
                {
                    if (_configs == null || !_configs.TryGetBuff(entry.BuffId, out var buff) || buff == null)
                    {
                        throw new InvalidOperationException($"Buff state continuous config not found. target={entry.TargetActorId} buffId={entry.BuffId}.");
                    }

                    if (_continuous == null)
                    {
                        throw new InvalidOperationException($"Buff state continuous manager is unavailable. target={entry.TargetActorId} buffId={entry.BuffId}.");
                    }

                    if (buff.ContinuousTagTemplateId > 0 && _tagTemplates == null)
                    {
                        throw new InvalidOperationException($"Buff state tag template registry is unavailable. target={entry.TargetActorId} buffId={entry.BuffId} template={buff.ContinuousTagTemplateId}.");
                    }
                }
            }

            return entries;
        }

        private void ApplyPrepared(FrameIndex frame, MobaBuffStateRecoveryEntry[] entries)
        {
            ClearAllBuffs(frame);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                _actors.TryGet(entry.TargetActorId, out var actor);
                var list = actor.hasBuffs && actor.buffs.Active != null
                    ? actor.buffs.Active
                    : new BuffRepository().GetOrCreateList(actor);
                RestoreEntry(frame, actor, in entry, list);
            }
        }

        private void ClearAllBuffs(FrameIndex frame)
        {
            var continuousBindings = new BuffContinuousBindingService(_continuous, _tags);
            foreach (var kv in _actors.Entries)
            {
                var actorId = kv.Key;
                var actor = kv.Value;
                if (actor == null || !actor.hasBuffs || actor.buffs.Active == null) continue;

                var active = actor.buffs.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    var runtime = active[i];
                    if (runtime == null) continue;
                    continuousBindings.Cleanup(actor, actorId, runtime, applyRemovalTags: false);
                    _runtimeContexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Destroyed, frame.Value);
                    _runtimeBindings.ReleaseSkillRuntime(runtime);
                    BuffRepository.ReleaseRuntime(runtime);
                }

                BuffRepository.ReleaseList(actor);
            }
        }

        private void RestoreEntry(FrameIndex frame, global::ActorEntity actor, in MobaBuffStateRecoveryEntry entry, List<BuffRuntime> list)
        {
            var runtime = BuffRepository.RentRuntime();
            var added = false;
            try
            {
                entry.ApplyTo(runtime);
                if (!TryReacquireSkillRuntime(runtime))
                {
                    throw new InvalidOperationException($"Buff state parent retain failed. target={entry.TargetActorId} buffId={entry.BuffId} runtime={runtime.SkillRuntimeHandle}.");
                }

                if (entry.HasContinuous)
                {
                    _configs.TryGetBuff(entry.BuffId, out var buff);
                    var requirements = BuffTagLifecycle.ResolveRequirements(buff, _tagTemplates);
                    runtime.TagRequirements = requirements;
                    var continuousBindings = new BuffContinuousBindingService(_continuous, _tags);
                    if (!continuousBindings.EnsureActive(runtime, buff, entry.SourceActorId, entry.TargetActorId, entry.RemainingSeconds, requirements))
                    {
                        throw new InvalidOperationException($"Buff state continuous activation failed. target={entry.TargetActorId} buffId={entry.BuffId} sourceContextId={entry.SourceContextId}.");
                    }
                }

                list.Add(runtime);
                added = true;
                BuffRepository.RegisterRuntime(list, runtime);
                _runtimeContexts.EnsureBuffContext(
                    runtime,
                    MobaBuffRuntimeContextData.FromRuntime(runtime, entry.TargetActorId, frame.Value, MobaRuntimeContextLifecycleState.Active));
                runtime = null;
            }
            finally
            {
                if (runtime != null)
                {
                    if (added && list.Remove(runtime)) BuffRepository.MarkDirty(list);
                    new BuffContinuousBindingService(_continuous, _tags).Cleanup(actor, entry.TargetActorId, runtime, applyRemovalTags: false);
                    _runtimeContexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Destroyed, frame.Value);
                    _runtimeBindings.ReleaseSkillRuntime(runtime);
                    BuffRepository.ReleaseRuntime(runtime);
                    if (list.Count == 0) BuffRepository.ReleaseList(actor);
                }
            }
        }

        private bool TryReacquireSkillRuntime(BuffRuntime runtime)
        {
            if (runtime == null) return false;
            var runtimeHandle = runtime.SkillRuntimeHandle;
            if (!runtimeHandle.IsValid) return true;
            if (runtime.SourceContextId == 0L) return false;

            var child = new MobaSkillRuntimeChildRef(
                MobaSkillRuntimeChildKind.Buff,
                runtime.SourceContextId,
                runtime.SourceContextId,
                runtime.BuffId);
            if (!_skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle)) return false;

            new BuffRuntimeView(runtime).BindSkillRuntime(in runtimeHandle, in retainHandle);
            return true;
        }

        private static int CompareEntries(MobaBuffStateRecoveryEntry a, MobaBuffStateRecoveryEntry b)
        {
            var c = a.TargetActorId.CompareTo(b.TargetActorId);
            if (c != 0) return c;
            c = a.BuffId.CompareTo(b.BuffId);
            if (c != 0) return c;
            c = a.SourceActorId.CompareTo(b.SourceActorId);
            if (c != 0) return c;
            return a.SourceContextId.CompareTo(b.SourceContextId);
        }

        private static bool SameIdentity(in MobaBuffStateRecoveryEntry a, in MobaBuffStateRecoveryEntry b)
        {
            return a.TargetActorId == b.TargetActorId
                && a.BuffId == b.BuffId
                && a.SourceActorId == b.SourceActorId
                && a.SourceContextId == b.SourceContextId;
        }

        private static bool EntriesEqual(in MobaBuffStateRecoveryEntry a, in MobaBuffStateRecoveryEntry b)
        {
            return SameIdentity(in a, in b)
                && a.RemainingSeconds.Equals(b.RemainingSeconds)
                && a.IntervalRemainingSeconds.Equals(b.IntervalRemainingSeconds)
                && a.StackCount == b.StackCount
                && a.RuntimeContextId == b.RuntimeContextId
                && a.RuntimeContextVersion == b.RuntimeContextVersion
                && a.OriginSourceActorId == b.OriginSourceActorId
                && a.OriginTargetActorId == b.OriginTargetActorId
                && a.OriginTraceKind == b.OriginTraceKind
                && a.OriginConfigId == b.OriginConfigId
                && a.OriginImmediateContextId == b.OriginImmediateContextId
                && a.OriginParentContextId == b.OriginParentContextId
                && a.OriginRootContextId == b.OriginRootContextId
                && a.OriginOwnerContextId == b.OriginOwnerContextId
                && a.SkillRuntimeId == b.SkillRuntimeId
                && a.SkillRuntimeGeneration == b.SkillRuntimeGeneration
                && a.SkillRuntimeRootTraceContextId == b.SkillRuntimeRootTraceContextId
                && a.HasContinuous == b.HasContinuous;
        }

        private static void AddEntryHash(in MobaBuffStateRecoveryEntry entry, ref MobaStateHashBuilder hash)
        {
            hash.AddInt(entry.TargetActorId);
            hash.AddInt(entry.BuffId);
            hash.AddFloat(entry.RemainingSeconds);
            hash.AddFloat(entry.IntervalRemainingSeconds);
            hash.AddInt(entry.SourceActorId);
            hash.AddInt(entry.StackCount);
            hash.AddLong(entry.SourceContextId);
            hash.AddLong(entry.RuntimeContextId);
            hash.AddLong(entry.RuntimeContextVersion);
            hash.AddInt(entry.OriginSourceActorId);
            hash.AddInt(entry.OriginTargetActorId);
            hash.AddInt(entry.OriginTraceKind);
            hash.AddInt(entry.OriginConfigId);
            hash.AddLong(entry.OriginImmediateContextId);
            hash.AddLong(entry.OriginParentContextId);
            hash.AddLong(entry.OriginRootContextId);
            hash.AddLong(entry.OriginOwnerContextId);
            hash.AddLong(entry.SkillRuntimeId);
            hash.AddInt(entry.SkillRuntimeGeneration);
            hash.AddLong(entry.SkillRuntimeRootTraceContextId);
            hash.AddInt(entry.HasContinuous ? 1 : 0);
        }


    }



    [MemoryPackable]
    public readonly partial struct MobaBuffStateRecoveryPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaBuffStateRecoveryEntry[] Entries;

        public MobaBuffStateRecoveryPayload(int version, MobaBuffStateRecoveryEntry[] entries)
        {
            Version = version;
            Entries = entries ?? Array.Empty<MobaBuffStateRecoveryEntry>();
        }
    }


    [MemoryPackable]
    public readonly partial struct MobaBuffStateRecoveryEntry
    {
        [MemoryPackOrder(0)] public readonly int TargetActorId;
        [MemoryPackOrder(1)] public readonly int BuffId;
        [MemoryPackOrder(2)] public readonly float RemainingSeconds;
        [MemoryPackOrder(3)] public readonly float IntervalRemainingSeconds;
        [MemoryPackOrder(4)] public readonly int SourceActorId;
        [MemoryPackOrder(5)] public readonly int StackCount;
        [MemoryPackOrder(6)] public readonly long SourceContextId;
        [MemoryPackOrder(7)] public readonly long RuntimeContextId;
        [MemoryPackOrder(8)] public readonly long RuntimeContextVersion;
        [MemoryPackOrder(9)] public readonly int OriginSourceActorId;
        [MemoryPackOrder(10)] public readonly int OriginTargetActorId;
        [MemoryPackOrder(11)] public readonly int OriginTraceKind;
        [MemoryPackOrder(12)] public readonly int OriginConfigId;
        [MemoryPackOrder(13)] public readonly long OriginImmediateContextId;
        [MemoryPackOrder(14)] public readonly long OriginParentContextId;
        [MemoryPackOrder(15)] public readonly long OriginRootContextId;
        [MemoryPackOrder(16)] public readonly long OriginOwnerContextId;
        [MemoryPackOrder(17)] public readonly long SkillRuntimeId;
        [MemoryPackOrder(18)] public readonly int SkillRuntimeGeneration;
        [MemoryPackOrder(19)] public readonly long SkillRuntimeRootTraceContextId;
        [MemoryPackOrder(20)] public readonly bool HasContinuous;

        public MobaSkillCastRuntimeHandle SkillRuntimeHandle =>
            SkillRuntimeId != 0L && SkillRuntimeGeneration > 0
                ? new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillRuntimeGeneration, SkillRuntimeRootTraceContextId)
                : default;

        public MobaBuffStateRecoveryEntry(
            int targetActorId,
            int buffId,
            float remainingSeconds,
            float intervalRemainingSeconds,
            int sourceActorId,
            int stackCount,
            long sourceContextId,
            long runtimeContextId,
            long runtimeContextVersion,
            int originSourceActorId,
            int originTargetActorId,
            int originTraceKind,
            int originConfigId,
            long originImmediateContextId,
            long originParentContextId,
            long originRootContextId,
            long originOwnerContextId,
            long skillRuntimeId,
            int skillRuntimeGeneration,
            long skillRuntimeRootTraceContextId,
            bool hasContinuous = false)
        {
            TargetActorId = targetActorId;
            BuffId = buffId;
            RemainingSeconds = remainingSeconds;
            IntervalRemainingSeconds = intervalRemainingSeconds;
            SourceActorId = sourceActorId;
            StackCount = stackCount;
            SourceContextId = sourceContextId;
            RuntimeContextId = runtimeContextId;
            RuntimeContextVersion = runtimeContextVersion;
            OriginSourceActorId = originSourceActorId;
            OriginTargetActorId = originTargetActorId;
            OriginTraceKind = originTraceKind;
            OriginConfigId = originConfigId;
            OriginImmediateContextId = originImmediateContextId;
            OriginParentContextId = originParentContextId;
            OriginRootContextId = originRootContextId;
            OriginOwnerContextId = originOwnerContextId;
            SkillRuntimeId = skillRuntimeId;
            SkillRuntimeGeneration = skillRuntimeGeneration;
            SkillRuntimeRootTraceContextId = skillRuntimeRootTraceContextId;
            HasContinuous = hasContinuous;
        }

        public static MobaBuffStateRecoveryEntry FromRuntime(int targetActorId, BuffRuntime runtime)
        {
            var origin = runtime.Origin;
            var skill = runtime.SkillRuntimeHandle.IsValid ? runtime.SkillRuntimeHandle : origin.SkillRuntimeHandle;
            return new MobaBuffStateRecoveryEntry(
                targetActorId,
                runtime.BuffId,
                runtime.Remaining,
                runtime.IntervalRemainingSeconds,
                runtime.SourceId,
                runtime.StackCount,
                runtime.SourceContextId,
                runtime.RuntimeContextId,
                runtime.RuntimeContextVersion,
                origin.SourceActorId,
                origin.TargetActorId,
                (int)origin.ImmediateKind,
                origin.ImmediateConfigId,
                origin.ImmediateContextId,
                origin.ParentContextId,
                origin.RootContextId,
                origin.OwnerContextId,
                skill.RuntimeId,
                skill.Generation,
                skill.RootTraceContextId,
                runtime.Continuous != null && !runtime.Continuous.IsTerminated);
        }

        public void ApplyTo(BuffRuntime runtime)
        {
            if (runtime == null) return;

            runtime.BuffId = BuffId;
            runtime.Remaining = RemainingSeconds;
            runtime.IntervalRemainingSeconds = IntervalRemainingSeconds;
            runtime.SourceId = SourceActorId;
            runtime.StackCount = StackCount;
            runtime.SourceContextId = SourceContextId;
            runtime.RuntimeContextId = RuntimeContextId;
            runtime.RuntimeContextVersion = RuntimeContextVersion;

            var skill = SkillRuntimeHandle;

            runtime.Origin = new MobaGameplayOrigin(
                OriginSourceActorId,
                OriginTargetActorId,
                (MobaTraceKind)OriginTraceKind,
                OriginConfigId,
                OriginImmediateContextId,
                OriginParentContextId,
                OriginRootContextId,
                OriginOwnerContextId,
                skill);
            runtime.ContextSource = MobaContextSourceView.FromOrigin(runtime.Origin, MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary.Snapshot, hasLiveRuntime: false, runtimeKind: "Buff", runtimeConfigId: BuffId);
            runtime.SkillRuntimeHandle = skill;
            runtime.SkillRuntimeRetainHandle = default;
            runtime.Continuous = null;
            runtime.TagRequirements = null;
            runtime.ModifierBindings?.Clear();
            runtime.ModifierBindings = null;
        }
    }

}

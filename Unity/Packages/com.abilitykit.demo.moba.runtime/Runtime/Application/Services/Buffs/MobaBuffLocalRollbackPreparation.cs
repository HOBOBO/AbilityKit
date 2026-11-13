using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Services.Buffs
{
    // Local rollback metadata; deliberately separate from the network Buff recovery DTO.
    internal sealed class MobaBuffLocalRollbackEntry
    {
        [JsonProperty(Required = Required.Always)] public int TargetActorId;
        [JsonProperty(Required = Required.Always)] public int BuffId;
        [JsonProperty(Required = Required.Always)] public int SourceActorId;
        [JsonProperty(Required = Required.Always)] public long SourceContextId;
        [JsonProperty(Required = Required.Always)] public long RetainId;
        [JsonProperty(Required = Required.Always)] public MobaBuffContextSourceCheckpoint ContextSource;
        [JsonProperty(Required = Required.Always)] public bool HasContinuous;
        [JsonProperty(Required = Required.Always)] public int ModifierSourceId;
        [JsonProperty(Required = Required.AllowNull)] public long? DurationRaw;
        [JsonProperty(Required = Required.Always)] public long ElapsedRaw;
        [JsonProperty(Required = Required.Always)] public long IntervalRemainingRaw;
        [JsonProperty(Required = Required.Always)] public int State;
        [JsonProperty(Required = Required.AllowNull)] public string Configuration;
    }

    // Explicit local DTO: do not rely on JSON assigning readonly runtime structs.
    internal sealed class MobaBuffContextSourceCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public int ResolveKind, Boundary, ContextKind, ExecutionKind;
        [JsonProperty(Required = Required.Always)] public int SourceActorId, TargetActorId, ConfigId, TriggerId, Frame, RuntimeConfigId;
        [JsonProperty(Required = Required.Always)] public long SourceContextId, ParentContextId, RootContextId, OwnerContextId;
        [JsonProperty(Required = Required.AllowNull)] public string RuntimeKind;
        [JsonProperty(Required = Required.Always)] public bool HasLiveRuntime;
        [JsonProperty(Required = Required.Always)] public long SkillRuntimeId, SkillRootContextId;
        [JsonProperty(Required = Required.Always)] public int SkillGeneration;

        public static MobaBuffContextSourceCheckpoint Capture(in MobaContextSourceView value) => new MobaBuffContextSourceCheckpoint
        {
            ResolveKind = (int)value.ResolveKind, Boundary = (int)value.Boundary,
            ContextKind = (int)value.ContextKind, ExecutionKind = (int)value.ExecutionKind,
            SourceActorId = value.SourceActorId, TargetActorId = value.TargetActorId,
            ConfigId = value.ConfigId, TriggerId = value.TriggerId, Frame = value.Frame,
            RuntimeConfigId = value.RuntimeConfigId, SourceContextId = value.SourceContextId,
            ParentContextId = value.ParentContextId, RootContextId = value.RootContextId,
            OwnerContextId = value.OwnerContextId, RuntimeKind = value.RuntimeKind,
            HasLiveRuntime = value.HasLiveRuntime, SkillRuntimeId = value.SkillRuntimeHandle.RuntimeId,
            SkillRootContextId = value.SkillRuntimeHandle.RootContextId, SkillGeneration = value.SkillRuntimeHandle.Generation,
        };

        public MobaContextSourceView ToView() => new MobaContextSourceView(
            (MobaContextSourceResolveKind)ResolveKind, (MobaContextSourceBoundary)Boundary,
            (EffectContextKind)ContextKind, (MobaExecutionKind)ExecutionKind,
            SourceActorId, TargetActorId, SourceContextId, ParentContextId, RootContextId, OwnerContextId,
            ConfigId, TriggerId, Frame, RuntimeKind, RuntimeConfigId, HasLiveRuntime,
            new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillGeneration, SkillRootContextId));
    }

    public sealed partial class MobaBuffStateRecoveryProvider
    {
        internal void ValidateLocalLifecycleOwnership()
        {
            if (!(_continuous is DefaultContinuousManager manager))
                throw new InvalidOperationException("Local Buff rollback requires explicit Continuous registration ownership.");
            var registered = new HashSet<IContinuous>(manager.GetAllContinuous());
            var owned = new HashSet<IContinuous>();
            foreach (var actor in _actors.Entries)
            {
                if (actor.Value == null || !actor.Value.hasBuffs || actor.Value.buffs.Active == null) continue;
                foreach (var runtime in actor.Value.buffs.Active)
                {
                    if (runtime == null) continue;
                    _runtimeContexts.ValidateBuffContextOwnership(runtime);
                    var continuous = runtime.Continuous;
                    if (continuous == null || continuous.IsTerminated) continue;
                    if (!registered.Contains(continuous) || !owned.Add(continuous) ||
                        !ReferenceEquals(continuous.Runtime, runtime) || continuous.BuffId != runtime.BuffId ||
                        continuous.SourceActorId != runtime.SourceId || continuous.TargetActorId != actor.Key ||
                        continuous.SourceContextId != runtime.SourceContextId)
                        throw new InvalidOperationException("Live Buff and Continuous ownership is inconsistent.");
                }
            }
            foreach (var continuous in registered)
                if (continuous is BuffContinuousRuntime && !owned.Contains(continuous))
                    throw new InvalidOperationException("Registered Buff Continuous has no repository owner.");
        }

        internal MobaBuffLocalRollbackEntry[] CaptureLocalRollbackEntries()
        {
            var entries = new List<MobaBuffLocalRollbackEntry>();
            foreach (var pair in _actors.Entries)
            {
                if (pair.Value == null || !pair.Value.hasBuffs || pair.Value.buffs.Active == null) continue;
                foreach (var runtime in pair.Value.buffs.Active)
                {
                    if (runtime == null || runtime.BuffId <= 0) continue;
                    var continuous = runtime.Continuous;
                    var hasContinuous = continuous != null && !continuous.IsTerminated;
                    entries.Add(new MobaBuffLocalRollbackEntry
                    {
                        TargetActorId = pair.Key, BuffId = runtime.BuffId,
                        SourceActorId = runtime.SourceId, SourceContextId = runtime.SourceContextId,
                        RetainId = runtime.SkillRuntimeRetainHandle.IsValid ? runtime.SkillRuntimeRetainHandle.RetainId : 0,
                        ContextSource = MobaBuffContextSourceCheckpoint.Capture(runtime.ContextSource), HasContinuous = hasContinuous,
                        ModifierSourceId = hasContinuous ? continuous.ModifierSourceId : 0,
                        DurationRaw = hasContinuous ? continuous.DurationRaw : null,
                        ElapsedRaw = hasContinuous ? continuous.ElapsedRaw : 0,
                        IntervalRemainingRaw = hasContinuous ? continuous.IntervalRemainingRaw : 0,
                        State = hasContinuous ? (int)continuous.State : 0,
                        Configuration = hasContinuous ? CaptureLocalBuffConfiguration(runtime.BuffId) : null,
                    });
                }
            }
            entries.Sort((a, b) => CompareEntries(ToIdentity(a), ToIdentity(b)));
            return entries.ToArray();
        }

        internal IReadOnlyList<BuffContinuousRuntime> PrepareLocalRollback(byte[] state,
            MobaBuffLocalRollbackEntry[] metadata, in MobaSkillRuntimeRollbackPayload targetSkills,
            IReadOnlyDictionary<long, MobaExecutionContextNode> executionNodes, ISet<long> contextIds)
        {
            if (metadata == null) throw new InvalidOperationException("Local Buff metadata is missing.");
            var byIdentity = new Dictionary<(int, int, int, long), MobaBuffLocalRollbackEntry>();
            foreach (var entry in metadata)
                if (entry == null || !byIdentity.TryAdd((entry.TargetActorId, entry.BuffId, entry.SourceActorId, entry.SourceContextId), entry))
                    throw new InvalidOperationException("Duplicate or invalid local Buff metadata.");
            var targetRetains = new Dictionary<long, MobaSkillRuntimeRetainRollbackEntry>();
            foreach (var retain in targetSkills.Retains ?? Array.Empty<MobaSkillRuntimeRetainRollbackEntry>())
                if (!targetRetains.TryAdd(retain.RetainId, retain))
                    throw new InvalidOperationException("Duplicate target skill retain.");
            var usedRetains = new HashSet<long>();
            var entries = ParseAndValidate(state, entry =>
            {
                if (!byIdentity.TryGetValue((entry.TargetActorId, entry.BuffId, entry.SourceActorId, entry.SourceContextId), out var local))
                    return false;
                // An unretained handle is provenance, not ownership of the Buff lifetime.
                if (local.RetainId == 0) return true;
                return targetRetains.TryGetValue(local.RetainId, out var retain) &&
                    retain.RuntimeId == entry.SkillRuntimeId && retain.Generation == entry.SkillRuntimeGeneration &&
                    retain.RootContextId == entry.SkillRuntimeRootContextId &&
                    retain.ChildKind == (int)MobaSkillRuntimeChildKind.Buff && retain.ChildId == entry.SourceContextId &&
                    retain.ChildContextId == entry.SourceContextId && retain.ChildConfigId == entry.BuffId;
            });
            if (entries.Length != metadata.Length) throw new InvalidOperationException("Local Buff metadata membership differs from Buff state.");
            var staged = new List<BuffContinuousRuntime>();
            var sourceIds = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (!byIdentity.TryGetValue((entry.TargetActorId, entry.BuffId, entry.SourceActorId, entry.SourceContextId), out var local) ||
                    local.RetainId < 0 || local.ContextSource == null || local.HasContinuous != entry.HasContinuous ||
                    local.RetainId != 0 && (!entry.SkillRuntimeHandle.IsValid || !usedRetains.Add(local.RetainId)) ||
                    entry.RuntimeContextId != 0 && !contextIds.Contains(entry.RuntimeContextId) ||
                    entry.StackCount < 1 || float.IsNaN(entry.RemainingSeconds) || entry.RemainingSeconds < 0 ||
                    float.IsNaN(entry.IntervalRemainingSeconds) || float.IsInfinity(entry.IntervalRemainingSeconds))
                    throw new InvalidOperationException("Local Buff state, retain, or context identity is invalid.");
                if (MobaExecutionContextRegistry.IsExecutionContextId(entry.SourceContextId) &&
                    (!executionNodes.TryGetValue(entry.SourceContextId, out var sourceNode) ||
                     sourceNode.Kind != MobaExecutionKind.BuffApply || sourceNode.ConfigId != entry.BuffId ||
                     sourceNode.SourceActorId != entry.SourceActorId || sourceNode.TargetActorId != entry.TargetActorId))
                    throw new InvalidOperationException("Local Buff source execution context is missing or mismatched.");
                ValidateLocalContextSource(local.ContextSource, entry);
                if (!entry.HasContinuous)
                {
                    if (local.ModifierSourceId != 0 || local.DurationRaw.HasValue || local.ElapsedRaw != 0 ||
                        local.IntervalRemainingRaw != 0 || local.State != 0 || local.Configuration != null)
                        throw new InvalidOperationException("Absent Buff Continuous has unexpected local state.");
                    continue;
                }
                if (local.ElapsedRaw < 0 || local.IntervalRemainingRaw < 0 ||
                    local.DurationRaw.HasValue && (local.DurationRaw.Value <= 0 || local.DurationRaw.Value < local.ElapsedRaw) ||
                    local.State != (int)ContinuousState.Active && local.State != (int)ContinuousState.Paused ||
                    !string.Equals(local.Configuration, CaptureLocalBuffConfiguration(entry.BuffId), StringComparison.Ordinal))
                    throw new InvalidOperationException("Local Buff Continuous timing, state, or configuration is invalid.");
                _configs.TryGetBuff(entry.BuffId, out var config);
                var hasModifiers = config.Modifiers != null && config.Modifiers.Count > 0;
                if (hasModifiers != (local.ModifierSourceId > 0) || local.ModifierSourceId < 0 ||
                    local.ModifierSourceId > 0 && !sourceIds.Add(local.ModifierSourceId))
                    throw new InvalidOperationException("Invalid local Buff modifier source identity.");
                // This object remains isolated. No activation, registration, context or ID allocation.
                var remaining = local.DurationRaw.HasValue
                    ? Deterministic.Fixed64.FromRaw(local.DurationRaw.Value - local.ElapsedRaw).ToSingle()
                    : float.PositiveInfinity;
                if (!remaining.Equals(entry.RemainingSeconds) ||
                    !Deterministic.Fixed64.FromRaw(local.IntervalRemainingRaw).ToSingle().Equals(entry.IntervalRemainingSeconds))
                    throw new InvalidOperationException("Local Buff fixed timing differs from its state views.");
                var planned = new BuffContinuousRuntime(config, entry.SourceActorId, entry.TargetActorId,
                    entry.RemainingSeconds, BuffTagLifecycle.ResolveRequirements(config, _tagTemplates));
                planned.BindSourceContext(entry.SourceContextId);
                if (local.ModifierSourceId > 0) planned.AssignModifierSourceId(local.ModifierSourceId);
                var runtime = new Components.BuffRuntime();
                entry.ApplyTo(runtime);
                runtime.ContextSource = local.ContextSource.ToView();
                runtime.Continuous = planned;
                runtime.TagRequirements = planned.TagRequirements;
                planned.BindRuntime(runtime);
                planned.RefreshRaw(entry.SourceActorId, local.DurationRaw, entry.StackCount, config.MaxStacks, planned.TagRequirements);
                planned.RestoreLocalDuration(local.DurationRaw);
                planned.RestoreLocalLifecycle((ContinuousState)local.State, local.ElapsedRaw, local.IntervalRemainingRaw);
                staged.Add(planned);
            }
            foreach (var retain in targetRetains.Values)
                if (retain.ChildKind == (int)MobaSkillRuntimeChildKind.Buff && !usedRetains.Contains(retain.RetainId))
                    throw new InvalidOperationException("Target Buff retain has no matching Buff metadata.");
            return staged.AsReadOnly();
        }

        private static void ValidateLocalContextSource(MobaBuffContextSourceCheckpoint source, in MobaBuffStateRecoveryEntry entry)
        {
            var view = source.ToView();
            if (!Enum.IsDefined(typeof(MobaContextSourceResolveKind), view.ResolveKind) ||
                !Enum.IsDefined(typeof(MobaContextSourceBoundary), view.Boundary) ||
                !Enum.IsDefined(typeof(EffectContextKind), view.ContextKind) ||
                source.ExecutionKind < 0 || source.ExecutionKind > byte.MaxValue ||
                !Enum.IsDefined(typeof(MobaExecutionKind), view.ExecutionKind) || source.Frame < 0 ||
                view.IsValid && (view.SourceActorId != entry.SourceActorId || view.TargetActorId != entry.TargetActorId ||
                    view.SourceContextId != entry.SourceContextId || view.ConfigId != entry.BuffId ||
                    !view.SkillRuntimeHandle.Equals(entry.SkillRuntimeHandle)))
                throw new InvalidOperationException("Local Buff context source differs from its state identity.");
        }

        private string CaptureLocalBuffConfiguration(int buffId)
        {
            if (_configs == null || !_configs.TryGetBuff(buffId, out var buff) || buff == null)
                throw new InvalidOperationException($"Local Buff configuration is missing: {buffId}.");
            return JsonConvert.SerializeObject(buff);
        }

        private static MobaBuffStateRecoveryEntry ToIdentity(MobaBuffLocalRollbackEntry value) =>
            new MobaBuffStateRecoveryEntry(value.TargetActorId, value.BuffId, 0, 0, value.SourceActorId, 0,
                value.SourceContextId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTriggerIntervalContinuousService))]
    public sealed class MobaTriggerIntervalContinuousService : IService, IWorldInitializable, IDisposable
    {
        private readonly Dictionary<ProcessKey, MobaTriggerIntervalContinuousRuntime> _activeByKey = new Dictionary<ProcessKey, MobaTriggerIntervalContinuousRuntime>();
        private readonly Dictionary<long, List<ProcessKey>> _keysByOwnerContext = new Dictionary<long, List<ProcessKey>>();

        private IWorldResolver _services;
        private MobaConfigDatabase _configs;
        private IContinuousManager _continuous;
        private IMobaContinuousTagTemplateRegistry _tagTemplates;
        private MobaCombatActivityService _combatActivity;

        internal readonly struct RollbackEntry
        {
            public RollbackEntry(int processId, int sourceActorId, int targetActorId,
                long ownerContextId, int modifierSourceId, long elapsedRaw, long intervalRemainingRaw,
                bool paused, MobaContextSourceView source)
            {
                ProcessId = processId;
                SourceActorId = sourceActorId;
                TargetActorId = targetActorId;
                OwnerContextId = ownerContextId;
                ModifierSourceId = modifierSourceId;
                ElapsedRaw = elapsedRaw;
                IntervalRemainingRaw = intervalRemainingRaw;
                Paused = paused;
                Source = source;
            }

            public int ProcessId { get; }
            public int SourceActorId { get; }
            public int TargetActorId { get; }
            public long OwnerContextId { get; }
            public int ModifierSourceId { get; }
            public long ElapsedRaw { get; }
            public long IntervalRemainingRaw { get; }
            public bool Paused { get; }
            public MobaContextSourceView Source { get; }
        }

        internal List<RollbackEntry> CaptureRollbackEntries()
        {
            var entries = new List<RollbackEntry>(_activeByKey.Count);
            var registered = (ResolveContinuous() as DefaultContinuousManager)?.GetAllContinuous();
            if (registered == null)
                throw new InvalidOperationException("Trigger interval rollback requires a continuous manager with registration order.");
            foreach (var continuous in registered)
            {
                if (!(continuous is MobaTriggerIntervalContinuousRuntime runtime) ||
                    runtime.IsTerminated || !OwnsRuntime(runtime)) continue;
                entries.Add(new RollbackEntry(runtime.ProcessId, runtime.SourceActorId,
                    runtime.TargetActorId, runtime.SourceContextId, runtime.ModifierSourceId,
                    runtime.ElapsedRaw, runtime.IntervalRemainingRaw, runtime.IsPaused,
                    runtime.SourceSnapshot));
            }
            return entries;
        }

        internal void ValidateRollbackEntries(IReadOnlyList<RollbackEntry> entries)
        {
            if (_configs == null || ResolveContinuous() == null)
                throw new InvalidOperationException("Trigger interval rollback dependencies are unavailable.");
            var keys = new HashSet<ProcessKey>();
            var sourceIds = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry.ProcessId <= 0 || entry.SourceActorId <= 0 || entry.TargetActorId <= 0 ||
                    entry.OwnerContextId == 0 || entry.ElapsedRaw < 0 || entry.IntervalRemainingRaw < 0 ||
                    !keys.Add(new ProcessKey(entry.OwnerContextId, entry.ProcessId,
                        entry.SourceActorId, entry.TargetActorId)) ||
                    !_configs.TryGetContinuousProcess(entry.ProcessId, out var process) || process == null)
                    throw new InvalidOperationException("Invalid trigger interval rollback entry.");
                var hasModifiers = process.Modifiers != null && process.Modifiers.Count > 0;
                if (hasModifiers != (entry.ModifierSourceId > 0) ||
                    entry.ModifierSourceId > 0 && !sourceIds.Add(entry.ModifierSourceId))
                    throw new InvalidOperationException("Invalid trigger interval modifier source identity.");
                ResolveRequirements(process);
            }
        }

        internal int GetRollbackModifierCount(int processId)
        {
            if (_configs == null || !_configs.TryGetContinuousProcess(processId, out var process) || process == null)
                throw new InvalidOperationException($"Trigger interval rollback config is missing: {processId}.");
            return process.Modifiers?.Count ?? 0;
        }

        internal bool OwnsRuntime(MobaTriggerIntervalContinuousRuntime runtime)
        {
            if (runtime == null) return false;
            var key = new ProcessKey(runtime.SourceContextId, runtime.ProcessId,
                runtime.SourceActorId, runtime.TargetActorId);
            return _activeByKey.TryGetValue(key, out var owned) && ReferenceEquals(owned, runtime);
        }

        internal void RestoreRollbackEntries(IReadOnlyList<RollbackEntry> entries)
        {
            ValidateRollbackEntries(entries);
            if (TryRestoreExistingEntries(entries)) return;
            var previous = CaptureRollbackEntries();
            try { ApplyRollbackEntries(entries); }
            catch (Exception failure)
            {
                try { ApplyRollbackEntries(previous); }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException("Trigger interval restore and rollback both failed.",
                        failure, rollbackFailure);
                }
                throw new InvalidOperationException("Trigger interval restore failed; previous instances were restored.", failure);
            }
        }

        private bool TryRestoreExistingEntries(IReadOnlyList<RollbackEntry> entries)
        {
            var current = CaptureRollbackEntries();
            if (current.Count != entries.Count) return false;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var key = new ProcessKey(entry.OwnerContextId, entry.ProcessId,
                    entry.SourceActorId, entry.TargetActorId);
                if (!_activeByKey.TryGetValue(key, out var runtime) || runtime == null ||
                    runtime.IsTerminated || runtime.ModifierSourceId != entry.ModifierSourceId ||
                    runtime.IsPaused != entry.Paused || !runtime.SourceSnapshot.Equals(entry.Source))
                    return false;
            }
            foreach (var entry in entries)
            {
                var key = new ProcessKey(entry.OwnerContextId, entry.ProcessId,
                    entry.SourceActorId, entry.TargetActorId);
                var runtime = _activeByKey[key];
                runtime.SetElapsedRaw(entry.ElapsedRaw);
                runtime.IntervalRemainingRaw = entry.IntervalRemainingRaw;
            }
            return true;
        }

        private void ApplyRollbackEntries(IReadOnlyList<RollbackEntry> entries)
        {
            var previous = new List<ProcessKey>(_activeByKey.Keys);
            foreach (var key in previous) EndProcess(key, ContinuousEndReason.CleanedUp);
            _activeByKey.Clear();
            _keysByOwnerContext.Clear();
            foreach (var entry in entries)
            {
                _configs.TryGetContinuousProcess(entry.ProcessId, out var process);
                var runtime = new MobaTriggerIntervalContinuousRuntime(process, entry.SourceActorId,
                    entry.TargetActorId, entry.OwnerContextId, ResolveRequirements(process), entry.Source);
                if (entry.ModifierSourceId > 0) runtime.AssignModifierSourceId(entry.ModifierSourceId);
                runtime.SetElapsedRaw(entry.ElapsedRaw);
                runtime.IntervalRemainingRaw = entry.IntervalRemainingRaw;
                if (!_continuous.TryActivate(runtime))
                    throw new InvalidOperationException("Trigger interval rollback activation failed.");
                if (entry.Paused) _continuous.TryPause(runtime);
                var key = new ProcessKey(entry.OwnerContextId, entry.ProcessId,
                    entry.SourceActorId, entry.TargetActorId);
                _activeByKey.Add(key, runtime);
                AddOwnerIndex(entry.OwnerContextId, key);
            }
        }
 
        public void OnInit(IWorldResolver services)
        {
            _services = services;
            services?.TryResolve(out _configs);
            services?.TryResolve(out _tagTemplates);
            services?.TryResolve(out _combatActivity);
        }

        public bool UpsertProcess(int processId, int sourceActorId, int targetActorId, long ownerContextId, in MobaContextSourceView source)
        {
            if (processId <= 0 || sourceActorId <= 0 || ownerContextId == 0) return false;
            if (_configs == null || ResolveContinuous() == null) return false;
            if (!_configs.TryGetContinuousProcess(processId, out var process) || process == null)
            {
                Log.Warning($"[MobaTriggerIntervalContinuousService] Continuous process config not found. processId={processId} source={sourceActorId} target={targetActorId} ownerContext={ownerContextId}");
                return false;
            }

            var resolvedTargetActorId = targetActorId > 0 ? targetActorId : sourceActorId;
            var key = new ProcessKey(ownerContextId, processId, sourceActorId, resolvedTargetActorId);
            if (_activeByKey.TryGetValue(key, out var existing) && existing != null && !existing.IsTerminated)
            {
                if (!CanKeepActive(process, resolvedTargetActorId))
                {
                    EndProcess(key, ContinuousEndReason.Interrupted);
                    return false;
                }

                return existing.IsActive;
            }

            if (!CanKeepActive(process, resolvedTargetActorId)) return false;

            var requirements = ResolveRequirements(process);
            var runtime = new MobaTriggerIntervalContinuousRuntime(process, sourceActorId, resolvedTargetActorId, ownerContextId, requirements, source);
            if (!_continuous.TryActivate(runtime))
            {
                Log.Warning($"[MobaTriggerIntervalContinuousService] Continuous process activation rejected. processId={processId} source={sourceActorId} target={resolvedTargetActorId} ownerContext={ownerContextId}");
                return false;
            }

            _activeByKey[key] = runtime;
            AddOwnerIndex(ownerContextId, key);
            return true;
        }

        public void ReconcileOwner(long ownerContextId)
        {
            if (ownerContextId == 0) return;
            if (!_keysByOwnerContext.TryGetValue(ownerContextId, out var keys) || keys == null || keys.Count == 0) return;

            for (int i = keys.Count - 1; i >= 0; i--)
            {
                var key = keys[i];
                if (!_activeByKey.TryGetValue(key, out var runtime) || runtime == null || runtime.IsTerminated)
                {
                    _activeByKey.Remove(key);
                    keys.RemoveAt(i);
                    continue;
                }

                var process = runtime.Process;
                if (process != null && !CanKeepActive(process, runtime.TargetActorId))
                {
                    EndProcess(key, ContinuousEndReason.Interrupted);
                    keys.RemoveAt(i);
                }
            }

            if (keys.Count == 0) _keysByOwnerContext.Remove(ownerContextId);
        }

        public void EndOwnerProcesses(long ownerContextId, ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
        {
            if (ownerContextId == 0) return;
            if (!_keysByOwnerContext.TryGetValue(ownerContextId, out var keys) || keys == null || keys.Count == 0) return;

            var snapshot = new ProcessKey[keys.Count];
            keys.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
            {
                EndProcess(snapshot[i], reason);
            }

            _keysByOwnerContext.Remove(ownerContextId);
        }

        public void EndMissingOwnerProcesses(long ownerContextId, IReadOnlyCollection<int> desiredProcessIds, ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
        {
            if (ownerContextId == 0) return;
            if (!_keysByOwnerContext.TryGetValue(ownerContextId, out var keys) || keys == null || keys.Count == 0) return;

            for (int i = keys.Count - 1; i >= 0; i--)
            {
                var key = keys[i];
                if (ContainsProcessId(desiredProcessIds, key.ProcessId)) continue;

                EndProcess(key, reason);
                keys.RemoveAt(i);
            }

            if (keys.Count == 0) _keysByOwnerContext.Remove(ownerContextId);
        }

        public void Dispose()
        {
            foreach (var kv in _activeByKey)
            {
                if (kv.Value != null && !kv.Value.IsTerminated)
                {
                    _continuous?.TryEnd(kv.Value, ContinuousEndReason.CleanedUp);
                }
            }

            _activeByKey.Clear();
            _keysByOwnerContext.Clear();
            _services = null;
            _configs = null;
            _continuous = null;
            _tagTemplates = null;
            _combatActivity = null;
        }

        private IContinuousManager ResolveContinuous()
        {
            if (_continuous == null)
            {
                _services?.TryResolve(out _continuous);
            }

            return _continuous;
        }

        private static bool ContainsProcessId(IReadOnlyCollection<int> processIds, int processId)
        {
            if (processIds == null || processId <= 0) return false;
            foreach (var id in processIds)
            {
                if (id == processId) return true;
            }

            return false;
        }

        private ContinuousTagRequirements ResolveRequirements(ContinuousProcessMO process)
        {
            if (process == null || process.ContinuousTagTemplateId <= 0) return new ContinuousTagRequirements();
            if (_tagTemplates == null || !_tagTemplates.TryGet(process.ContinuousTagTemplateId, out var requirements) || requirements == null)
                throw new System.InvalidOperationException($"Continuous process {process.Id} references missing tag template {process.ContinuousTagTemplateId}.");
            return requirements;
        }

        private bool CanKeepActive(ContinuousProcessMO process, int actorId)
        {
            if (process == null) return false;
            if (!process.RequireOutOfCombat) return true;
            return _combatActivity != null && _combatActivity.IsOutOfCombat(actorId, process.OutOfCombatSeconds);
        }

        private void EndProcess(ProcessKey key, ContinuousEndReason reason)
        {
            if (!_activeByKey.TryGetValue(key, out var runtime) || runtime == null)
            {
                _activeByKey.Remove(key);
                return;
            }

            if (!runtime.IsTerminated)
            {
                ResolveContinuous()?.TryEnd(runtime, reason);
            }

            _activeByKey.Remove(key);
        }

        private void AddOwnerIndex(long ownerContextId, ProcessKey key)
        {
            if (!_keysByOwnerContext.TryGetValue(ownerContextId, out var keys) || keys == null)
            {
                keys = new List<ProcessKey>(2);
                _keysByOwnerContext[ownerContextId] = keys;
            }

            if (!keys.Contains(key)) keys.Add(key);
        }

        private readonly struct ProcessKey : IEquatable<ProcessKey>
        {
            public readonly long OwnerContextId;
            public readonly int ProcessId;
            public readonly int SourceActorId;
            public readonly int TargetActorId;

            public ProcessKey(long ownerContextId, int processId, int sourceActorId, int targetActorId)
            {
                OwnerContextId = ownerContextId;
                ProcessId = processId;
                SourceActorId = sourceActorId;
                TargetActorId = targetActorId;
            }

            public bool Equals(ProcessKey other)
            {
                return OwnerContextId == other.OwnerContextId
                    && ProcessId == other.ProcessId
                    && SourceActorId == other.SourceActorId
                    && TargetActorId == other.TargetActorId;
            }

            public override bool Equals(object obj)
            {
                return obj is ProcessKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = OwnerContextId.GetHashCode();
                    hash = hash * 397 ^ ProcessId;
                    hash = hash * 397 ^ SourceActorId;
                    hash = hash * 397 ^ TargetActorId;
                    return hash;
                }
            }
        }
    }
}


using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Continuous;
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
            return _tagTemplates != null && _tagTemplates.TryGet(process.ContinuousTagTemplateId, out var requirements) && requirements != null
                ? requirements
                : new ContinuousTagRequirements();
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


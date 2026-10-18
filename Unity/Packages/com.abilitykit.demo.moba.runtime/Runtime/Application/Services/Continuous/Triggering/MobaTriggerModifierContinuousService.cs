using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTriggerModifierContinuousService))]
    public sealed class MobaTriggerModifierContinuousService : IService, IWorldInitializable, IDisposable
    {
        private readonly Dictionary<(int OwnerId, int LegacySourceId), TriggerModifierContinuous> _active =
            new Dictionary<(int OwnerId, int LegacySourceId), TriggerModifierContinuous>();
        private MobaContinuousManager _continuous;
        private int _nextProjectionId = int.MinValue;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _continuous);
        }

        public bool Add(int ownerActorId, int legacySourceId, int parameterId, ModifierOp op, float value, int priority)
        {
            if (_continuous == null || ownerActorId <= 0 || legacySourceId == 0 ||
                !MobaSkillParamModifierKeys.IsTriggerWritable(parameterId) ||
                !MobaSkillParamModifierKeys.SupportsOperation(parameterId, (int)op) ||
                !MobaSkillParamModifierKeys.TryValidateValue(parameterId, value, out _)) return false;

            var key = (ownerActorId, legacySourceId);
            var spec = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = parameterId,
                Op = (int)op,
                Value = value,
                Priority = priority,
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.Realtime,
            });
            if (_active.TryGetValue(key, out var existing) && !existing.IsTerminated)
            {
                existing.Specs.Add(spec);
                try
                {
                    _continuous.Reproject(existing);
                    return true;
                }
                catch
                {
                    existing.Specs.RemoveAt(existing.Specs.Count - 1);
                    _continuous.Reproject(existing);
                    throw;
                }
            }

            var runtime = new TriggerModifierContinuous(ownerActorId, legacySourceId,
                AllocateProjectionId(ownerActorId));
            runtime.Specs.Add(spec);
            if (!_continuous.TryActivate(runtime)) return false;
            _active[key] = runtime;
            return true;
        }

        public bool Remove(int ownerActorId, int legacySourceId)
        {
            var key = (ownerActorId, legacySourceId);
            if (!_active.TryGetValue(key, out var runtime)) return false;
            _active.Remove(key);
            if (runtime.IsTerminated) return true;
            return _continuous != null && _continuous.TryEnd(runtime, ContinuousEndReason.CleanedUp);
        }

        public void RemoveOwner(int ownerActorId)
        {
            if (ownerActorId <= 0) return;
            var sourceIds = new List<int>();
            foreach (var key in _active.Keys)
                if (key.OwnerId == ownerActorId) sourceIds.Add(key.LegacySourceId);
            sourceIds.Sort();
            for (var i = 0; i < sourceIds.Count; i++)
                Remove(ownerActorId, sourceIds[i]);
        }

        public void Dispose()
        {
            foreach (var runtime in _active.Values)
                if (!runtime.IsTerminated) _continuous?.TryEnd(runtime, ContinuousEndReason.CleanedUp);
            _active.Clear();
            _continuous = null;
        }

        internal int NextProjectionId => _nextProjectionId;
        internal IReadOnlyList<TriggerModifierContinuous> GetActive()
        {
            var result = new List<TriggerModifierContinuous>();
            foreach (var runtime in _active.Values)
                if (!runtime.IsTerminated) result.Add(runtime);
            result.Sort((left, right) =>
            {
                var order = left.OwnerActorId.CompareTo(right.OwnerActorId);
                return order != 0 ? order : left.LegacySourceId.CompareTo(right.LegacySourceId);
            });
            return result;
        }

        internal void Restore(IReadOnlyList<TriggerModifierContinuous> runtimes, int nextProjectionId)
        {
            if (_continuous == null) throw new InvalidOperationException("Continuous manager is unavailable.");
            DisposeActive();
            _nextProjectionId = nextProjectionId;
            foreach (var runtime in runtimes)
            {
                if (!_continuous.TryActivate(runtime))
                    throw new InvalidOperationException("Trigger modifier continuous activation was rejected during rollback.");
                _active.Add((runtime.OwnerActorId, runtime.LegacySourceId), runtime);
                if (runtime.WasPaused && !_continuous.TryPause(runtime))
                    throw new InvalidOperationException("Trigger modifier continuous pause was rejected during rollback.");
            }
        }

        private void DisposeActive()
        {
            foreach (var runtime in _active.Values)
                if (!runtime.IsTerminated) _continuous.TryEnd(runtime, ContinuousEndReason.CleanedUp);
            _active.Clear();
        }

        private int AllocateProjectionId(int ownerActorId)
        {
            while (_nextProjectionId != 0)
            {
                var candidate = _nextProjectionId++;
                var used = false;
                foreach (var continuous in _continuous.GetAllContinuous())
                {
                    if (continuous?.Config is IMobaContinuousProjectionConfig projection &&
                        projection.OwnerActorId == ownerActorId && projection.ModifierSourceId == candidate)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return candidate;
            }
            throw new InvalidOperationException("Trigger modifier projection IDs are exhausted.");
        }
    }

    internal sealed class TriggerModifierContinuous : MobaContinuousRuntimeBase
    {
        private readonly ConfigData _config;

        public TriggerModifierContinuous(int ownerActorId, int legacySourceId, int projectionId)
        {
            OwnerActorId = ownerActorId;
            LegacySourceId = legacySourceId;
            ProjectionId = projectionId;
            Specs = new List<IMobaContinuousModifierSpec>();
            _config = new ConfigData(this);
        }

        public int OwnerActorId { get; }
        public int LegacySourceId { get; }
        public int ProjectionId { get; }
        public bool WasPaused { get; set; }
        public List<IMobaContinuousModifierSpec> Specs { get; }
        public override IContinuousConfig Config => _config;

        private sealed class ConfigData : MobaContinuousConfigBase
        {
            private readonly TriggerModifierContinuous _runtime;

            public ConfigData(TriggerModifierContinuous runtime) : base(0f, null, runtime.Specs)
            {
                _runtime = runtime;
            }

            public override string Id => $"trigger_modifier:{_runtime.OwnerActorId}:{_runtime.LegacySourceId}:{_runtime.ProjectionId}";
            public override long OwnerId => _runtime.OwnerActorId;
            public override int OwnerActorId => _runtime.OwnerActorId;
            public override int ModifierSourceId => _runtime.ProjectionId;
            public override GameplayTagSource TagSource => GameplayTagSource.System;
        }
    }
}

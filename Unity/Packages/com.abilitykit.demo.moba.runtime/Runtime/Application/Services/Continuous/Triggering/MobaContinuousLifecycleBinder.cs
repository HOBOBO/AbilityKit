using System;
using System.Collections.Generic;
using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousLifecycleBinder : IContinuousLifecycleBinder
    {
        private readonly MobaContinuousModifierProjectorRegistry _modifierProjectors;
        private readonly Dictionary<(int OwnerActorId, int SourceId), IContinuous> _owners =
            new Dictionary<(int OwnerActorId, int SourceId), IContinuous>();
        private int _nextSourceId = 1;

        internal int NextSourceId => _nextSourceId;

        internal void RestoreNextSourceId(int nextSourceId)
        {
            ValidateNextSourceId(nextSourceId);
            _nextSourceId = nextSourceId;
        }

        internal void ValidateNextSourceId(int nextSourceId)
        {
            if (nextSourceId <= 0) throw new InvalidOperationException("Invalid modifier source allocator state.");
            foreach (var pair in _owners)
                if (pair.Value is MobaContinuousRuntimeBase runtime &&
                    runtime.AllocatedModifierSourceId == pair.Key.SourceId &&
                    pair.Key.SourceId >= nextSourceId)
                    throw new InvalidOperationException("Modifier source allocator state precedes an active source.");
        }

        public MobaContinuousLifecycleBinder(MobaContinuousModifierProjectorRegistry modifierProjectors)
        {
            _modifierProjectors = modifierProjectors;
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
            if (!(continuous?.Config is IMobaContinuousProjectionConfig projection) ||
                !(continuous.Config is IMobaContinuousModifierConfig modifiers) ||
                modifiers.Modifiers == null || modifiers.Modifiers.Count == 0 ||
                projection.OwnerActorId <= 0) return;

            if (projection.ModifierSourceId == 0 && continuous is MobaContinuousRuntimeBase runtime)
                runtime.AssignModifierSourceId(AllocateSourceId(projection.OwnerActorId));
            if (projection.ModifierSourceId == 0)
                throw new InvalidOperationException("Continuous modifier projection has no source identity.");

            var key = (OwnerActorId: projection.OwnerActorId, SourceId: projection.ModifierSourceId);
            if (_owners.TryGetValue(key, out var existing) && !ReferenceEquals(existing, continuous))
                throw new InvalidOperationException($"Modifier source collision: owner={key.OwnerActorId}, source={key.SourceId}.");
            _owners[key] = continuous;
        }

        private int AllocateSourceId(int ownerActorId)
        {
            while (_nextSourceId > 0)
            {
                if (_nextSourceId == int.MaxValue)
                    throw new InvalidOperationException("Continuous modifier source IDs are exhausted.");
                var candidate = _nextSourceId++;
                if (!_owners.ContainsKey((ownerActorId, candidate))) return candidate;
            }
            throw new InvalidOperationException("Continuous modifier source IDs are exhausted.");
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            ApplyModifiers(continuous);
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
            ClearModifiers(continuous);
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
            try { ApplyModifiers(continuous); }
            catch (Exception failure)
            {
                Exception cleanupFailure = null;
                try
                {
                    if (continuous != null && !continuous.IsTerminated)
                        manager.TryEnd(continuous, ContinuousEndReason.CleanedUp);
                }
                catch (Exception error) { cleanupFailure = error; }
                try { manager.Unregister(continuous, ContinuousEndReason.CleanedUp); }
                catch (Exception error)
                {
                    cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error);
                }
                if (cleanupFailure != null)
                    throw new AggregateException("Modifier resume and continuous cleanup both failed.",
                        failure, cleanupFailure);
                throw;
            }
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            ClearModifiers(continuous);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            ClearModifiers(continuous);
            if (continuous?.Config is IMobaContinuousProjectionConfig projection)
            {
                var key = (projection.OwnerActorId, projection.ModifierSourceId);
                if (_owners.TryGetValue(key, out var existing) && ReferenceEquals(existing, continuous))
                    _owners.Remove(key);
            }
            (continuous as MobaContinuousRuntimeBase)?.ClearModifierCaptures();
        }

        public void Reproject(IContinuous continuous)
        {
            ClearModifiers(continuous);
            if (continuous != null && continuous.IsActive && !continuous.IsTerminated)
            {
                ApplyModifiers(continuous);
            }
        }

        public void ReprojectTimeVarying(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsActive || continuous.IsTerminated ||
                !(continuous.Config is IMobaContinuousProjectionConfig projection) ||
                !(continuous.Config is IMobaContinuousModifierConfig modifiers)) return;
            _modifierProjectors?.ReprojectTimeVarying(continuous, projection, modifiers.Modifiers);
        }

        private void ApplyModifiers(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsActive || continuous.IsTerminated) return;
            if (!(continuous.Config is IMobaContinuousProjectionConfig projection)) return;
            if (!(continuous.Config is IMobaContinuousModifierConfig modifiers)) return;

            _modifierProjectors?.Apply(continuous, projection, modifiers.Modifiers);
        }

        private void ClearModifiers(IContinuous continuous)
        {
            if (continuous == null) return;
            if (!(continuous.Config is IMobaContinuousProjectionConfig projection)) return;
            var key = (projection.OwnerActorId, projection.ModifierSourceId);
            if (!_owners.TryGetValue(key, out var registered) || !ReferenceEquals(registered, continuous)) return;

            _modifierProjectors?.Clear(projection);
        }
    }
}

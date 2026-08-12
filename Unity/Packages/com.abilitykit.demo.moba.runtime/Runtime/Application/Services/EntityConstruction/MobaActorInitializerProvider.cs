using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public readonly struct MobaActorInitializationContext
    {
        public readonly global::ActorEntity Entity;
        public readonly MobaPlayerLoadout Loadout;
        public readonly MobaResolvedHeroLoadout ResolvedLoadout;

        public MobaActorInitializationContext(
            global::ActorEntity entity,
            in MobaPlayerLoadout loadout,
            in MobaResolvedHeroLoadout resolvedLoadout)
        {
            Entity = entity;
            Loadout = loadout;
            ResolvedLoadout = resolvedLoadout;
        }
    }

    public interface IMobaActorInitializerStep
    {
        string Id { get; }
        int Order { get; }

        bool TryPrepare(
            in MobaActorInitializationContext context,
            out object preparedState,
            out string error);

        void Apply(in MobaActorInitializationContext context, object preparedState);
    }

    /// <summary>
    /// Immutable ordered provider. Core steps are always present and extensions cannot
    /// replace their ids or order slots. Every step is prepared before any step is applied.
    /// </summary>
    public sealed class MobaActorInitializerProvider
    {
        private readonly IMobaActorInitializerStep[] _steps;

        public IReadOnlyList<IMobaActorInitializerStep> Steps => _steps;

        public MobaActorInitializerProvider(
            IEnumerable<IMobaActorInitializerStep> coreSteps,
            IEnumerable<IMobaActorInitializerStep> extensionSteps = null)
        {
            if (coreSteps == null) throw new ArgumentNullException(nameof(coreSteps));

            var steps = new List<IMobaActorInitializerStep>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            AddSteps(coreSteps, "core", steps, ids, orders);
            if (steps.Count == 0)
            {
                throw new InvalidOperationException("At least one core actor initializer is required.");
            }

            AddSteps(extensionSteps, "extension", steps, ids, orders);
            steps.Sort((left, right) => left.Order.CompareTo(right.Order));
            _steps = steps.ToArray();
        }

        public bool TryInitialize(in MobaActorInitializationContext context, out string error)
        {
            error = null;
            var preparedStates = new object[_steps.Length];

            for (var i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];
                try
                {
                    if (!step.TryPrepare(in context, out preparedStates[i], out error))
                    {
                        if (string.IsNullOrEmpty(error))
                        {
                            error = $"actor initializer prepare failed: {step.Id}";
                        }
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    error = $"actor initializer prepare failed: {step.Id}; {ex.Message}";
                    return false;
                }
            }

            for (var i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];
                try
                {
                    step.Apply(in context, preparedStates[i]);
                }
                catch (Exception ex)
                {
                    error = $"actor initializer apply failed: {step.Id}; {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        private static void AddSteps(
            IEnumerable<IMobaActorInitializerStep> source,
            string sourceName,
            List<IMobaActorInitializerStep> steps,
            HashSet<string> ids,
            HashSet<int> orders)
        {
            if (source == null) return;

            foreach (var step in source)
            {
                if (step == null)
                {
                    throw new InvalidOperationException($"Null {sourceName} actor initializer is not allowed.");
                }
                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    throw new InvalidOperationException($"{sourceName} actor initializer id is required.");
                }
                if (!ids.Add(step.Id))
                {
                    throw new InvalidOperationException($"Actor initializer id conflict: {step.Id}");
                }
                if (!orders.Add(step.Order))
                {
                    throw new InvalidOperationException($"Actor initializer order conflict: {step.Order}");
                }

                steps.Add(step);
            }
        }
    }
}

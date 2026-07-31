using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFlowConfigurationValidationResult
    {
        internal MobaFlowConfigurationValidationResult(List<string> errors)
        {
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        internal IReadOnlyList<string> Errors { get; }
        internal bool IsValid => Errors.Count == 0;
    }

    internal static class MobaFlowConfigurationValidator
    {
        internal static MobaFlowConfigurationValidationResult Validate(MobaFlowConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var errors = new List<string>();
            ValidateStateMachine(configuration.RootMachine, errors);
            ValidateStateMachine(configuration.BattleMachine, errors);
            ValidateStateDescriptions(configuration.RootMachine.States, configuration.RootStateDescriptions, "root", errors);
            ValidateStateDescriptions(configuration.BattleMachine.States, configuration.BattleStateDescriptions, "battle", errors);
            ValidateFeatureSpecs(configuration, errors);
            return new MobaFlowConfigurationValidationResult(errors);
        }

        internal static void ValidateOrThrow(MobaFlowConfiguration configuration)
        {
            var result = Validate(configuration);
            if (result.IsValid) return;

            throw new InvalidOperationException(
                "MOBA flow configuration is incomplete: " + string.Join(" | ", result.Errors));
        }

        private static void ValidateStateMachine<TKey, TEvent>(
            PhaseStateMachineSpec<TKey, TEvent> machine,
            List<string> errors)
            where TKey : notnull
            where TEvent : notnull
        {
            var result = new PhaseStateMachineValidator<TKey, TEvent>().Validate(machine);
            for (var i = 0; i < result.Errors.Count; i++)
            {
                errors.Add(result.Errors[i]);
            }
        }

        private static void ValidateStateDescriptions<TState>(
            IReadOnlyList<TState> configuredStates,
            IReadOnlyDictionary<TState, string> descriptions,
            string stateKind,
            List<string> errors)
            where TState : struct, Enum
        {
            var enumStates = Enum.GetValues(typeof(TState));
            for (var i = 0; i < enumStates.Length; i++)
            {
                var state = (TState)enumStates.GetValue(i);
                if (!Contains(configuredStates, state))
                {
                    errors.Add($"MOBA {stateKind} state is not registered in its state machine: {state}");
                }

                if (!descriptions.TryGetValue(state, out var description) || string.IsNullOrWhiteSpace(description))
                {
                    errors.Add($"MOBA {stateKind} state has no stable description: {state}");
                }
            }

            foreach (var pair in descriptions)
            {
                if (!Contains(configuredStates, pair.Key))
                {
                    errors.Add($"MOBA {stateKind} description references an unregistered state: {pair.Key}");
                }
            }
        }

        private static void ValidateFeatureSpecs(MobaFlowConfiguration configuration, List<string> errors)
        {
            ValidateFeatureSpec(configuration.BootFeatures, "Boot", errors);
            ValidateFeatureSpec(configuration.LobbyFeatures, "Lobby", errors);
            ValidateFeatureSpec(configuration.BattlePrepareFeatures, "Battle.Prepare", errors);
            ValidateFeatureSpec(configuration.BattleConnectFeatures, "Battle.Connect", errors);
            ValidateFeatureSpec(configuration.BattleCreateOrJoinWorldFeatures, "Battle.CreateOrJoinWorld", errors);
            ValidateFeatureSpec(configuration.BattleLoadAssetsFeatures, "Battle.LoadAssets", errors);
            ValidateFeatureSpec(configuration.BattleInMatchFeatures, "Battle.InMatch", errors);
            ValidateFeatureSpec(configuration.BattleEndFeatures, "Battle.End", errors);
        }

        private static void ValidateFeatureSpec(PhaseStateFeatureSpec spec, string expectedStateId, List<string> errors)
        {
            if (spec == null)
            {
                errors.Add($"MOBA flow state '{expectedStateId}' has no feature specification.");
                return;
            }

            if (!string.Equals(spec.StateId, expectedStateId, StringComparison.Ordinal))
            {
                errors.Add($"MOBA flow state '{expectedStateId}' has mismatched feature specification id: {spec.StateId}");
            }
        }

        private static bool Contains<T>(IReadOnlyList<T> values, T expected)
        {
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < values.Count; i++)
            {
                if (comparer.Equals(values[i], expected)) return true;
            }

            return false;
        }
    }
}

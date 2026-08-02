using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using AbilityKit.Demo.Moba.Services.StateMachine;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    public static class MobaBrainConfigurationValidator
    {
        public static void Validate(
            IMobaActorBrainCatalog brains,
            IMobaActorStateMachineProfileCatalog profiles,
            MobaActorStateMachineRuntimeRegistry runtimeRegistry,
            MobaBrainDecisionDriverRegistry decisionDrivers,
            ITextAssetLoader textAssetLoader = null)
        {
            if (brains == null) throw new ArgumentNullException(nameof(brains));
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (runtimeRegistry == null) throw new ArgumentNullException(nameof(runtimeRegistry));
            if (decisionDrivers == null) throw new ArgumentNullException(nameof(decisionDrivers));

            var errors = new List<string>();
            var profileList = profiles.Profiles;
            for (var i = 0; i < profileList.Count; i++)
            {
                ValidateProfile(profileList[i], runtimeRegistry, errors);
            }

            var definitions = brains.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                switch (definition.DriverKind)
                {
                    case MobaBrainDriverKind.BTree:
                        ValidateBTreeBrain(in definition, decisionDrivers, textAssetLoader, errors);
                        break;
                    case MobaBrainDriverKind.Hfsm:
                        if (!profiles.TryGet(definition.DecisionName, out _))
                        {
                            errors.Add(
                                $"Brain '{definition.BrainId}' references missing HFSM profile '{definition.DecisionName}'.");
                        }
                        break;
                    default:
                        errors.Add(
                            $"Brain '{definition.BrainId}' uses unsupported driver '{definition.DriverKind}'.");
                        break;
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "MOBA brain configuration validation failed:" + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.ConvertAll(error => "- " + error)));
            }
        }

        private static void ValidateBTreeBrain(
            in MobaActorBrainDefinition definition,
            MobaBrainDecisionDriverRegistry decisionDrivers,
            ITextAssetLoader textAssetLoader,
            List<string> errors)
        {
            if (!decisionDrivers.Contains(MobaBrainDriverKind.BTree))
            {
                errors.Add($"Brain '{definition.BrainId}' requires an unregistered BTree driver.");
                return;
            }

            if (!MobaBTreeAssetLoader.TryLoad(textAssetLoader, definition.DecisionName, out var json))
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' references missing BTree resource '{definition.DecisionName}'.");
                return;
            }

            try
            {
                MobaBTreeDecision.ValidateConfiguration(json);
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' BTree '{definition.DecisionName}' is invalid: {ex.Message}");
            }
        }

        private static void ValidateProfile(
            HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec> profile,
            MobaActorStateMachineRuntimeRegistry runtimeRegistry,
            List<string> errors)
        {
            if (profile == null)
            {
                errors.Add("HFSM profile catalog contains a null profile.");
                return;
            }

            ValidateStateMachine(
                profile.Id,
                profile.StartState,
                profile.States,
                profile.Transitions,
                runtimeRegistry,
                errors);
        }

        private static void ValidateStateMachine(
            string path,
            string startState,
            IReadOnlyList<HfsmRuntimeNodeSpec<MobaHfsmActionSpec>> states,
            IReadOnlyList<HfsmRuntimeTransitionSpec> transitions,
            MobaActorStateMachineRuntimeRegistry runtimeRegistry,
            List<string> errors)
        {
            var stateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionStateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            states ??= Array.Empty<HfsmRuntimeNodeSpec<MobaHfsmActionSpec>>();
            transitions ??= Array.Empty<HfsmRuntimeTransitionSpec>();

            if (states.Count == 0) errors.Add($"HFSM '{path}' contains no states.");
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                {
                    errors.Add($"HFSM '{path}' contains a null state at index {i}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.Id))
                {
                    errors.Add($"HFSM '{path}' contains a state with an empty id at index {i}.");
                    continue;
                }

                if (!stateIds.Add(state.Id))
                {
                    errors.Add($"HFSM '{path}' contains duplicate state id '{state.Id}'.");
                    continue;
                }

                if (state.Kind == HfsmRuntimeNodeKind.StateMachine)
                {
                    ValidateStateMachine(
                        path + "/" + state.Id,
                        state.StartState,
                        state.Children,
                        state.Transitions,
                        runtimeRegistry,
                        errors);
                }
                else
                {
                    actionStateIds.Add(state.Id);
                    ValidateBehaviour(path + "/" + state.Id, state.BehaviourRoot, runtimeRegistry, errors);
                }
            }

            if (string.IsNullOrWhiteSpace(startState) || !stateIds.Contains(startState))
            {
                errors.Add($"HFSM '{path}' start state '{startState}' does not exist.");
            }

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (!stateIds.Contains(transition.From))
                    errors.Add($"HFSM '{path}' transition[{i}] source '{transition.From}' does not exist.");
                if (!stateIds.Contains(transition.To))
                    errors.Add($"HFSM '{path}' transition[{i}] target '{transition.To}' does not exist.");
                if (transition.Mode != HfsmRuntimeTransitionMode.Condition
                    && !actionStateIds.Contains(transition.From))
                {
                    errors.Add(
                        $"HFSM '{path}' transition[{i}] result source '{transition.From}' is not an action state.");
                }
                if (!string.IsNullOrWhiteSpace(transition.Condition)
                    && !runtimeRegistry.ContainsCondition(transition.Condition))
                {
                    errors.Add(
                        $"HFSM '{path}' transition[{i}] condition '{transition.Condition}' is not registered.");
                }
            }
        }

        private static void ValidateBehaviour(
            string path,
            HfsmRuntimeBehaviourSpec<MobaHfsmActionSpec> behaviour,
            MobaActorStateMachineRuntimeRegistry runtimeRegistry,
            List<string> errors)
        {
            if (behaviour == null)
            {
                errors.Add($"HFSM action state '{path}' has no behavior root.");
                return;
            }

            switch (behaviour.Kind)
            {
                case HfsmRuntimeBehaviourKind.Action:
                    if (!runtimeRegistry.ContainsAction(behaviour.Action.Type))
                    {
                        errors.Add(
                            $"HFSM behavior '{path}' action '{behaviour.Action.Type}' is not registered.");
                    }
                    break;
                case HfsmRuntimeBehaviourKind.Condition:
                    if (!runtimeRegistry.ContainsCondition(behaviour.Condition))
                    {
                        errors.Add(
                            $"HFSM behavior '{path}' condition '{behaviour.Condition}' is not registered.");
                    }
                    break;
                case HfsmRuntimeBehaviourKind.Invert:
                case HfsmRuntimeBehaviourKind.Repeat:
                case HfsmRuntimeBehaviourKind.Timeout:
                    if (behaviour.Children.Count != 1)
                    {
                        errors.Add(
                            $"HFSM behavior '{path}' decorator '{behaviour.Kind}' requires exactly one child.");
                    }
                    break;
                case HfsmRuntimeBehaviourKind.Sequence:
                case HfsmRuntimeBehaviourKind.Selector:
                case HfsmRuntimeBehaviourKind.Parallel:
                    if (behaviour.Children.Count == 0)
                    {
                        errors.Add(
                            $"HFSM behavior '{path}' composite '{behaviour.Kind}' requires at least one child.");
                    }
                    break;
            }

            for (var i = 0; i < behaviour.Children.Count; i++)
            {
                ValidateBehaviour(path + "/" + behaviour.Kind + "[" + i + "]", behaviour.Children[i], runtimeRegistry, errors);
            }
        }
    }
}

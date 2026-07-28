using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using Newtonsoft.Json;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public static class MobaActorStateMachineProfileJsonLoader
    {
        public const string DefaultResourcePath = "moba/actor_state_machines";

        public static int Load(
            ITextAssetLoader loader,
            MobaActorStateMachineProfileCatalog catalog,
            string resourcePath = DefaultResourcePath)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!loader.TryLoadText(resourcePath, out var json) || string.IsNullOrWhiteSpace(json)) return 0;

            return LoadJson(json, catalog);
        }

        public static int LoadJson(string json, MobaActorStateMachineProfileCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrWhiteSpace(json)) return 0;

            var definitions = JsonConvert.DeserializeObject<List<ProfileDefinition>>(json)
                ?? throw new InvalidOperationException("MOBA actor state-machine profile JSON must be an array.");

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine profile at index {i} is null.");
                catalog.Register(ConvertProfile(definition));
            }

            return definitions.Count;
        }

        private static HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec> ConvertProfile(ProfileDefinition definition)
        {
            return new HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec>(
                definition.Id,
                definition.StartState,
                ConvertNodes(definition.States),
                ConvertTransitions(definition.Transitions));
        }

        private static IReadOnlyList<HfsmRuntimeNodeSpec<MobaHfsmActionSpec>> ConvertNodes(
            IReadOnlyList<NodeDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return Array.Empty<HfsmRuntimeNodeSpec<MobaHfsmActionSpec>>();

            var nodes = new HfsmRuntimeNodeSpec<MobaHfsmActionSpec>[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine node at index {i} is null.");
                var kind = definition.Kind ?? string.Empty;
                if (string.Equals(kind, "stateMachine", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kind, "machine", StringComparison.OrdinalIgnoreCase))
                {
                    nodes[i] = new HfsmRuntimeNodeSpec<MobaHfsmActionSpec>(
                        definition.Id,
                        definition.StartState,
                        ConvertNodes(definition.States),
                        ConvertTransitions(definition.Transitions),
                        definition.RememberLastState);
                    continue;
                }

                if (kind.Length > 0
                    && !string.Equals(kind, "actionState", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kind, "action", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"MOBA actor state-machine node '{definition.Id}' has unsupported kind '{definition.Kind}'.");
                }

                nodes[i] = new HfsmRuntimeNodeSpec<MobaHfsmActionSpec>(
                    definition.Id,
                    ConvertActions(definition.Actions),
                    definition.IntervalSeconds);
            }

            return nodes;
        }

        private static IReadOnlyList<MobaHfsmActionSpec> ConvertActions(IReadOnlyList<ActionDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) return Array.Empty<MobaHfsmActionSpec>();

            var actions = new MobaHfsmActionSpec[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine action at index {i} is null.");
                actions[i] = new MobaHfsmActionSpec(definition.Type, definition.Argument);
            }

            return actions;
        }

        private static IReadOnlyList<HfsmRuntimeTransitionSpec> ConvertTransitions(
            IReadOnlyList<TransitionDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) return Array.Empty<HfsmRuntimeTransitionSpec>();

            var transitions = new HfsmRuntimeTransitionSpec[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine transition at index {i} is null.");
                transitions[i] = new HfsmRuntimeTransitionSpec(
                    definition.From,
                    definition.To,
                    definition.Condition);
            }

            return transitions;
        }

        private sealed class ProfileDefinition
        {
            public string Id { get; set; }
            public string StartState { get; set; }
            public List<NodeDefinition> States { get; set; }
            public List<TransitionDefinition> Transitions { get; set; }
        }

        private sealed class NodeDefinition
        {
            public string Id { get; set; }
            public string Kind { get; set; }
            public string StartState { get; set; }
            public float IntervalSeconds { get; set; }
            public bool RememberLastState { get; set; }
            public List<ActionDefinition> Actions { get; set; }
            public List<NodeDefinition> States { get; set; }
            public List<TransitionDefinition> Transitions { get; set; }
        }

        private sealed class ActionDefinition
        {
            public string Type { get; set; }
            public string Argument { get; set; }
        }

        private sealed class TransitionDefinition
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Condition { get; set; }
        }
    }
}

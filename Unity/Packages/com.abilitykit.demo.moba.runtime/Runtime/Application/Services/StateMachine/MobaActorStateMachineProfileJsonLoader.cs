using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using Newtonsoft.Json;
using AbilityKit.HFSM.Extension;

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

            var definitions = JsonConvert.DeserializeObject<List<ProfileDefinition>>(
                json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error })
                ?? throw new InvalidOperationException("MOBA actor state-machine profile JSON must be an array.");

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine profile at index {i} is null.");
                catalog.Register(ConvertProfile(definition));
            }

            return definitions.Count;
        }

        private static HierarchicalProfile<MobaHfsmActionSpec> ConvertProfile(ProfileDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new InvalidOperationException("MOBA actor state-machine profile requires a non-empty 'id'.");
            if (string.IsNullOrWhiteSpace(definition.StartState))
                throw new InvalidOperationException($"MOBA actor state-machine profile '{definition.Id}' requires 'startState'.");

            return new HierarchicalProfile<MobaHfsmActionSpec>(
                definition.Id,
                definition.StartState,
                ConvertNodes(definition.States),
                ConvertTransitions(definition.Transitions));
        }

        private static IReadOnlyList<NodeSpec<MobaHfsmActionSpec>> ConvertNodes(
            IReadOnlyList<NodeDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return Array.Empty<NodeSpec<MobaHfsmActionSpec>>();

            var nodes = new NodeSpec<MobaHfsmActionSpec>[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine node at index {i} is null.");
                var kind = definition.Kind ?? string.Empty;
                if (string.Equals(kind, "stateMachine", StringComparison.OrdinalIgnoreCase))
                {
                    if (definition.BehaviorRoot != null)
                    {
                        throw new InvalidOperationException(
                            $"MOBA actor state-machine node '{definition.Id}' cannot define a behavior root because it is a nested state machine.");
                    }
                    if (string.IsNullOrWhiteSpace(definition.StartState))
                        throw new InvalidOperationException(
                            $"MOBA nested state-machine node '{definition.Id}' requires 'startState'.");
                    if (definition.States == null || definition.States.Count == 0)
                        throw new InvalidOperationException(
                            $"MOBA nested state-machine node '{definition.Id}' requires non-empty 'states'.");

                    nodes[i] = new NodeSpec<MobaHfsmActionSpec>(
                        definition.Id,
                        definition.StartState,
                        ConvertNodes(definition.States),
                        ConvertTransitions(definition.Transitions),
                        definition.RememberLastState);
                    continue;
                }

                if (!string.Equals(kind, "actionState", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"MOBA actor state-machine node '{definition.Id}' has unsupported kind '{definition.Kind}'.");
                }

                if (definition.BehaviorRoot == null)
                    throw new InvalidOperationException(
                        $"MOBA action-state node '{definition.Id}' requires 'behaviorRoot'.");
                if (definition.States != null || definition.Transitions != null)
                    throw new InvalidOperationException(
                        $"MOBA action-state node '{definition.Id}' cannot define nested states or transitions.");

                nodes[i] = new NodeSpec<MobaHfsmActionSpec>(
                    definition.Id,
                    ConvertBehaviour(definition.BehaviorRoot, $"state '{definition.Id}' behaviorRoot"),
                    ParseCompletionPolicy(
                        definition.CompletionPolicy,
                        ActionStateCompletionPolicy.Hold),
                    definition.NeedsExitTime);
            }

            return nodes;
        }

        private static BehaviourSpec<MobaHfsmActionSpec> ConvertBehaviour(
            BehaviourDefinition definition,
            string path)
        {
            if (definition == null)
                throw new InvalidOperationException($"MOBA HFSM behavior at {path} is null.");

            var kind = (definition.Kind ?? string.Empty).Trim().ToLowerInvariant();
            switch (kind)
            {
                case "action":
                    RequireLeaf(definition, path, kind);
                    if (string.IsNullOrWhiteSpace(definition.Type))
                        throw new InvalidOperationException($"MOBA HFSM action at {path} requires a non-empty 'type'.");
                    return BehaviourSpec<MobaHfsmActionSpec>.Task(
                        new MobaHfsmActionSpec(definition.Type, definition.Argument));

                case "condition":
                    RequireLeaf(definition, path, kind);
                    if (string.IsNullOrWhiteSpace(definition.Condition))
                        throw new InvalidOperationException($"MOBA HFSM condition at {path} requires a non-empty 'condition'.");
                    return BehaviourSpec<MobaHfsmActionSpec>.ConditionNode(definition.Condition);

                case "delay":
                    RequireLeaf(definition, path, kind);
                    RequireNonNegativeDuration(definition.DurationSeconds, path, kind);
                    return BehaviourSpec<MobaHfsmActionSpec>.Delay(
                        definition.DurationSeconds,
                        definition.UseUnscaledTime);

                case "sequence":
                    var sequenceChildren = RequireCompositeChildren(definition, path, kind);
                    return BehaviourSpec<MobaHfsmActionSpec>.Sequence(
                        ConvertBehaviourChildren(sequenceChildren, path));

                case "selector":
                    var selectorChildren = RequireCompositeChildren(definition, path, kind);
                    return BehaviourSpec<MobaHfsmActionSpec>.Selector(
                        ConvertBehaviourChildren(selectorChildren, path));

                case "parallel":
                    var parallelChildren = RequireCompositeChildren(definition, path, kind);
                    return BehaviourSpec<MobaHfsmActionSpec>.Parallel(
                        ConvertBehaviourChildren(parallelChildren, path),
                        ParseParallelSuccessPolicy(definition.SuccessPolicy),
                        ParseParallelFailurePolicy(definition.FailurePolicy));

                case "invert":
                    return BehaviourSpec<MobaHfsmActionSpec>.Decorate(
                        BehaviourKind.Invert,
                        ConvertSingleBehaviourChild(definition, path, kind));

                case "repeat":
                    var repeatCount = definition.RepeatCount ?? -1;
                    if (repeatCount < -1)
                        throw new InvalidOperationException($"MOBA HFSM repeat at {path} has invalid repeatCount '{repeatCount}'.");
                    return BehaviourSpec<MobaHfsmActionSpec>.Decorate(
                        BehaviourKind.Repeat,
                        ConvertSingleBehaviourChild(definition, path, kind),
                        repeatCount: repeatCount);

                case "timeout":
                    RequireNonNegativeDuration(definition.DurationSeconds, path, kind);
                    return BehaviourSpec<MobaHfsmActionSpec>.Decorate(
                        BehaviourKind.Timeout,
                        ConvertSingleBehaviourChild(definition, path, kind),
                        durationSeconds: definition.DurationSeconds,
                        useUnscaledTime: definition.UseUnscaledTime);

                default:
                    throw new InvalidOperationException(
                        $"MOBA HFSM behavior at {path} has unsupported kind '{definition.Kind}'.");
            }
        }

        private static IReadOnlyList<BehaviourDefinition> RequireCompositeChildren(
            BehaviourDefinition definition,
            string path,
            string kind)
        {
            if (definition.Child != null)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} must use 'children', not 'child'.");
            if (definition.Children == null || definition.Children.Count == 0)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} requires non-empty 'children'.");
            return definition.Children;
        }

        private static BehaviourSpec<MobaHfsmActionSpec>[] ConvertBehaviourChildren(
            IReadOnlyList<BehaviourDefinition> definitions,
            string parentPath)
        {
            var children = new BehaviourSpec<MobaHfsmActionSpec>[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                children[i] = ConvertBehaviour(definitions[i], $"{parentPath}.children[{i}]");
            }

            return children;
        }

        private static BehaviourSpec<MobaHfsmActionSpec> ConvertSingleBehaviourChild(
            BehaviourDefinition definition,
            string path,
            string kind)
        {
            if (definition.Children != null)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} must use 'child', not 'children'.");
            if (definition.Child == null)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} requires 'child'.");
            return ConvertBehaviour(definition.Child, $"{path}.child");
        }

        private static void RequireLeaf(
            BehaviourDefinition definition,
            string path,
            string kind)
        {
            if (definition.Child != null || definition.Children != null)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} cannot contain child behaviors.");
        }

        private static void RequireNonNegativeDuration(float duration, string path, string kind)
        {
            if (duration < 0f)
                throw new InvalidOperationException($"MOBA HFSM {kind} at {path} has negative durationSeconds '{duration}'.");
        }

        private static IReadOnlyList<TransitionSpec> ConvertTransitions(
            IReadOnlyList<TransitionDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) return Array.Empty<TransitionSpec>();

            var transitions = new TransitionSpec[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA actor state-machine transition at index {i} is null.");
                transitions[i] = new TransitionSpec(
                    definition.From,
                    definition.To,
                    definition.Condition,
                    ParseTransitionMode(definition.Mode),
                    definition.Priority,
                    definition.ForceInstantly);
            }

            return transitions;
        }

        private static TransitionMode ParseTransitionMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return TransitionMode.Condition;
            return mode.Trim().ToLowerInvariant() switch
            {
                "condition" => TransitionMode.Condition,
                "onsucceeded" => TransitionMode.OnSucceeded,
                "onfailed" => TransitionMode.OnFailed,
                "onfinished" => TransitionMode.OnFinished,
                _ => throw new InvalidOperationException($"Unsupported HFSM transition mode '{mode}'."),
            };
        }

        private static ActionStateCompletionPolicy ParseCompletionPolicy(
            string policy,
            ActionStateCompletionPolicy defaultPolicy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return defaultPolicy;
            return policy.Trim().ToLowerInvariant() switch
            {
                "loop" => ActionStateCompletionPolicy.Loop,
                "hold" => ActionStateCompletionPolicy.Hold,
                _ => throw new InvalidOperationException($"Unsupported HFSM completion policy '{policy}'."),
            };
        }

        private static ParallelSuccessPolicy ParseParallelSuccessPolicy(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return ParallelSuccessPolicy.All;
            return policy.Trim().ToLowerInvariant() switch
            {
                "all" => ParallelSuccessPolicy.All,
                "any" => ParallelSuccessPolicy.Any,
                _ => throw new InvalidOperationException($"Unsupported HFSM parallel success policy '{policy}'."),
            };
        }

        private static ParallelFailurePolicy ParseParallelFailurePolicy(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return ParallelFailurePolicy.Any;
            return policy.Trim().ToLowerInvariant() switch
            {
                "any" => ParallelFailurePolicy.Any,
                "all" => ParallelFailurePolicy.All,
                _ => throw new InvalidOperationException($"Unsupported HFSM parallel failure policy '{policy}'."),
            };
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
            public bool RememberLastState { get; set; }
            public bool NeedsExitTime { get; set; }
            public string CompletionPolicy { get; set; }
            [JsonProperty("behaviorRoot")]
            public BehaviourDefinition BehaviorRoot { get; set; }
            public List<NodeDefinition> States { get; set; }
            public List<TransitionDefinition> Transitions { get; set; }
        }

        private sealed class BehaviourDefinition
        {
            public string Kind { get; set; }
            public string Type { get; set; }
            public string Argument { get; set; }
            public string Condition { get; set; }
            public BehaviourDefinition Child { get; set; }
            public List<BehaviourDefinition> Children { get; set; }
            public int? RepeatCount { get; set; }
            public float DurationSeconds { get; set; }
            public bool UseUnscaledTime { get; set; }
            public string SuccessPolicy { get; set; }
            public string FailurePolicy { get; set; }
        }

        private sealed class TransitionDefinition
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Condition { get; set; }
            public string Mode { get; set; }
            public int Priority { get; set; }
            public bool ForceInstantly { get; set; }
        }
    }
}

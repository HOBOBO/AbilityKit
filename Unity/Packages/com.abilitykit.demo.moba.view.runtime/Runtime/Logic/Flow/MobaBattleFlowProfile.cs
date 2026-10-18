using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Game.Flow
{
    [Serializable]
    public sealed class MobaBattleFlowFeatureGroup
    {
        public string GroupId = string.Empty;
        public string[] FeatureIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MobaBattleFlowFeatureRef
    {
        public string FeatureId = string.Empty;
        public string GroupId = string.Empty;
    }

    [Serializable]
    public sealed class MobaBattleFlowPhase
    {
        public string StateId = string.Empty;
        public string[] FeatureIds = Array.Empty<string>();
        public MobaBattleFlowFeatureRef[] FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
        public string[] RetainOnExitFeatureIds = Array.Empty<string>();
        public string[] EnterAfterActionIds = Array.Empty<string>();
        public string[] ExitActionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MobaBattleFlowTopologyState
    {
        public string StateId = string.Empty;
        public string Description = string.Empty;
    }

    [Serializable]
    public sealed class MobaBattleFlowTopologyTransition
    {
        public string EventId = string.Empty;
        public string FromStateId = string.Empty;
        public string ToStateId = string.Empty;
    }

    internal sealed class MobaBattleFlowTopology
    {
        public MobaBattleFlowTopology(
            string startStateId,
            IReadOnlyList<MobaBattleFlowTopologyState> states,
            IReadOnlyList<MobaBattleFlowTopologyTransition> transitions)
        {
            StartStateId = startStateId;
            States = states;
            Transitions = transitions;
        }

        public string StartStateId { get; }
        public IReadOnlyList<MobaBattleFlowTopologyState> States { get; }
        public IReadOnlyList<MobaBattleFlowTopologyTransition> Transitions { get; }
    }

    internal sealed class MobaBattleFlowComposition
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _featuresByState;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _retainedFeaturesByState;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _enterActionsByState;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _exitActionsByState;

        public MobaBattleFlowComposition(
            string profileId, IReadOnlyList<string> stateOrder,
            IReadOnlyDictionary<string, IReadOnlyList<string>> featuresByState,
            IReadOnlyDictionary<string, IReadOnlyList<string>> retainedFeaturesByState,
            IReadOnlyList<string> sessionModuleIds,
            IReadOnlyDictionary<string, IReadOnlyList<string>> enterActionsByState,
            IReadOnlyDictionary<string, IReadOnlyList<string>> exitActionsByState,
            MobaBattleFlowTopology topology)
        {
            ProfileId = profileId;
            StateOrder = stateOrder;
            _featuresByState = featuresByState;
            _retainedFeaturesByState = retainedFeaturesByState;
            SessionModuleIds = sessionModuleIds;
            _enterActionsByState = enterActionsByState;
            _exitActionsByState = exitActionsByState;
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        }

        public string ProfileId { get; }
        public IReadOnlyList<string> StateOrder { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> FeaturesByState => _featuresByState;
        public IReadOnlyList<string> SessionModuleIds { get; }
        public MobaBattleFlowTopology Topology { get; }
        public IReadOnlyList<string> GetEnterActions(string stateId) => _enterActionsByState[stateId];
        public IReadOnlyList<string> GetExitActions(string stateId) => _exitActionsByState[stateId];

        public void ValidateExtensions(MobaFlowExtensionRegistry registry)
        {
            foreach (var stateId in StateOrder)
            {
                foreach (var actionId in GetEnterActions(stateId)) Validate(actionId);
                foreach (var actionId in GetExitActions(stateId)) Validate(actionId);
            }
            void Validate(string actionId)
            {
                if (registry == null || !registry.Contains(actionId))
                    throw new InvalidOperationException($"Battle flow profile '{ProfileId}' references unregistered action: {actionId}");
            }
        }
        public IReadOnlyList<string> GetRetainedFeatures(string stateId) =>
            _retainedFeaturesByState.TryGetValue(stateId, out var features)
                ? features
                : throw new InvalidOperationException("Battle flow phase is missing: " + stateId);

        public IReadOnlyList<string> GetFeatures(string stateId) =>
            _featuresByState.TryGetValue(stateId, out var features)
                ? features
                : throw new InvalidOperationException("Battle flow phase is missing: " + stateId);

        public void ValidateFactories(MobaFeatureFactoryRegistry factories)
        {
            if (factories == null) throw new ArgumentNullException(nameof(factories));
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stateId in StateOrder)
            {
                foreach (var featureId in GetFeatures(stateId))
                {
                    if (!factories.Contains(featureId))
                        throw new InvalidOperationException(
                            $"Battle flow profile '{ProfileId}' references unregistered feature: {featureId}");
                    var contract = factories.GetContract(featureId);
                    if (contract.Lifetime == MobaFeatureLifetime.Battle &&
                        !Contains(GetRetainedFeatures(stateId), featureId))
                        throw new InvalidOperationException(
                            $"Battle feature '{featureId}' must be retained after {stateId}.");
                    foreach (var requiredId in contract.RequiredFeatureIds)
                        if (!active.Contains(requiredId))
                            throw new InvalidOperationException(
                                $"Battle feature '{featureId}' requires '{requiredId}' before {stateId}.");
                    foreach (var requiredPort in contract.RequiredPorts)
                    {
                        var available = false;
                        foreach (var activeId in active)
                            foreach (var providedPort in factories.GetContract(activeId).ProvidedPorts)
                                available |= requiredPort.IsAssignableFrom(providedPort);
                        if (!available)
                            throw new InvalidOperationException(
                                $"Battle feature '{featureId}' requires port {requiredPort.Name} before {stateId}.");
                    }
                    active.Add(featureId);
                }

                var retained = new HashSet<string>(GetRetainedFeatures(stateId), StringComparer.Ordinal);
                foreach (var featureId in GetFeatures(stateId))
                    if (!retained.Contains(featureId)) active.Remove(featureId);
            }

            static bool Contains(IReadOnlyList<string> ids, string id)
            {
                for (var i = 0; i < ids.Count; i++)
                    if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        /// <summary>
        /// 按副本声明的玩法端口组裁剪 feature 组合。声明了端口组的 feature 只有在其端口组
        /// 被本副本声明时才安装；未声明端口组的 feature 属于基础能力，始终安装。
        /// 裁剪后的组合仍然要通过 <see cref="ValidateFactories"/>，因此被裁掉的依赖会立即报错。
        /// </summary>
        public MobaBattleFlowComposition FilterByPortGroups(
            IReadOnlyList<string> activePortGroups, MobaFeatureFactoryRegistry factories)
        {
            if (factories == null) throw new ArgumentNullException(nameof(factories));
            var active = new HashSet<string>(StringComparer.Ordinal);
            if (activePortGroups != null)
                foreach (var group in activePortGroups)
                    if (!string.IsNullOrWhiteSpace(group)) active.Add(group);

            bool ShouldInstall(string featureId)
            {
                if (!factories.Contains(featureId)) return true;
                var contract = factories.GetContract(featureId);
                if (contract.SatisfiedPortGroups.Count == 0) return true;
                foreach (var group in contract.SatisfiedPortGroups)
                    if (active.Contains(group)) return true;
                return false;
            }

            var featuresByState = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var retainedByState = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var stateId in StateOrder)
            {
                var kept = new List<string>();
                foreach (var featureId in GetFeatures(stateId))
                    if (ShouldInstall(featureId)) kept.Add(featureId);
                featuresByState.Add(stateId, Array.AsReadOnly(kept.ToArray()));

                var keptRetained = new List<string>();
                foreach (var featureId in GetRetainedFeatures(stateId))
                    if (ShouldInstall(featureId)) keptRetained.Add(featureId);
                retainedByState.Add(stateId, Array.AsReadOnly(keptRetained.ToArray()));
            }

            return new MobaBattleFlowComposition(ProfileId, StateOrder,
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(featuresByState),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(retainedByState),
                SessionModuleIds, _enterActionsByState, _exitActionsByState, Topology);
        }
    }

    [Serializable]
    public sealed class MobaBattleFlowProfile
    {
        public const string FixedTopologyId = "moba.root-battle.v1";
        private static readonly string[] StandardSessionModules =
        {
            "session_events", "gateway_room", "snapshot_routing", "session_dispatchers",
            "session_editor_hooks", "session_lifecycle", "session_net_adapter",
            "session_replay", "session_tick_loop", "session_plan"
        };
        private static readonly string[] RequiredSessionCapabilities =
        {
            "events", "plan"
        };

        internal static IReadOnlyList<string> GetBuiltInModuleCapabilities(string moduleId)
        {
            switch (moduleId)
            {
                case "session_events": return new[] { "events" };
                case "gateway_room": return new[] { "room" };
                case "snapshot_routing": return new[] { "snapshots" };
                case "session_dispatchers": return new[] { "dispatch" };
                case "session_lifecycle": return new[] { "lifecycle" };
                case "session_net_adapter": return new[] { "network" };
                case "session_replay": return new[] { "replay" };
                case "session_tick_loop": return new[] { "tick" };
                case "session_plan": return new[] { "plan" };
                default: return Array.Empty<string>();
            }
        }

        internal static void ValidateSessionCapabilities(IReadOnlyList<string> moduleIds,
            Func<string, IReadOnlyList<string>> getCapabilities)
        {
            ValidateSessionCapabilities(moduleIds, getCapabilities, RequiredSessionCapabilities);
        }

        internal static void ValidateSessionCapabilities(IReadOnlyList<string> moduleIds,
            Func<string, IReadOnlyList<string>> getCapabilities, IReadOnlyList<string> requiredCapabilities)
        {
            var available = new HashSet<string>(StringComparer.Ordinal);
            foreach (var moduleId in moduleIds)
                foreach (var capability in getCapabilities(moduleId) ?? Array.Empty<string>())
                    available.Add(capability);
            foreach (var required in requiredCapabilities)
                if (!available.Contains(required))
                    throw new InvalidOperationException("Battle flow requires session capability: " + required);
        }

        public string ProfileId = string.Empty;
        public string TopologyId = FixedTopologyId;
        public string TopologyStartStateId = "Battle.Prepare";
        public MobaBattleFlowTopologyState[] TopologyStates = Array.Empty<MobaBattleFlowTopologyState>();
        public MobaBattleFlowTopologyTransition[] TopologyTransitions = Array.Empty<MobaBattleFlowTopologyTransition>();
        public int Version;
        public MobaBattleFlowFeatureGroup[] Groups = Array.Empty<MobaBattleFlowFeatureGroup>();
        public MobaBattleFlowPhase[] Phases = Array.Empty<MobaBattleFlowPhase>();
        public string[] SessionModuleIds = Array.Empty<string>();
        public string[] AllowedHostModes = Array.Empty<string>();

        public void ValidateHostMode(string hostMode)
        {
            var allowed = AllowedHostModes ?? Array.Empty<string>();
            if (allowed.Length == 0) return;
            if (string.IsNullOrWhiteSpace(hostMode) || Array.IndexOf(allowed, hostMode) < 0)
                throw new InvalidOperationException(
                    $"Battle flow profile '{ProfileId}' does not support host mode: {hostMode ?? "unknown"}");
        }

        public string[] GetFeatures(string stateId)
        {
            var features = Resolve().GetFeatures(stateId);
            var copy = new string[features.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = features[i];
            return copy;
        }

        public void ValidateOrThrow() => Resolve();

        internal MobaBattleFlowComposition Resolve()
        {
            if (string.IsNullOrWhiteSpace(ProfileId) || (Version < 1 || Version > 5) ||
                Phases == null)
                throw new InvalidOperationException("Battle flow profile header or phase count is invalid.");
            var topology = ResolveTopology();
            if (Version < 5 && !string.Equals(TopologyId, FixedTopologyId, StringComparison.Ordinal))
                throw new InvalidOperationException("Configurable battle topology requires profile version 5.");
            var allowedModes = AllowedHostModes ?? Array.Empty<string>();
            ValidateFeatureIds(allowedModes, "allowed host modes");
            foreach (var mode in allowedModes)
                if (mode != "Local" && mode != "GatewayRemote")
                    throw new InvalidOperationException("Battle flow profile has unknown host mode: " + mode);

            var groupDefinitions = Groups ?? Array.Empty<MobaBattleFlowFeatureGroup>();
            if (Version == 1 && groupDefinitions.Length != 0)
                throw new InvalidOperationException("Battle flow feature groups require profile version 2.");

            var groups = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var group in groupDefinitions)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.GroupId) || group.FeatureIds == null ||
                    !groups.TryAdd(group.GroupId, group.FeatureIds))
                    throw new InvalidOperationException("Battle flow profile contains an invalid or duplicate feature group.");
                ValidateFeatureIds(group.FeatureIds, "group '" + group.GroupId + "'");
            }

            var resolved = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var retainedByState = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var enterActionsByState = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var exitActionsByState = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var phase in Phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.StateId) ||
                    !ContainsState(topology.States, phase.StateId) ||
                    resolved.ContainsKey(phase.StateId))
                    throw new InvalidOperationException("Battle flow profile contains an invalid or duplicate phase.");

                var directFeatures = phase.FeatureIds ?? Array.Empty<string>();
                var references = phase.FeatureRefs ?? Array.Empty<MobaBattleFlowFeatureRef>();
                if (Version == 1 && references.Length != 0)
                    throw new InvalidOperationException("Battle flow feature references require profile version 2.");
                if (directFeatures.Length != 0 && references.Length != 0)
                    throw new InvalidOperationException(
                        "Battle flow phase cannot mix FeatureIds and FeatureRefs: " + phase.StateId);

                var features = new List<string>();
                var seenFeatures = new HashSet<string>(StringComparer.Ordinal);
                if (references.Length == 0)
                {
                    foreach (var featureId in directFeatures)
                        AddFeature(featureId, phase.StateId, features, seenFeatures);
                }
                else
                {
                    foreach (var reference in references)
                    {
                        if (reference == null ||
                            (string.IsNullOrWhiteSpace(reference.FeatureId) ==
                             string.IsNullOrWhiteSpace(reference.GroupId)))
                            throw new InvalidOperationException(
                                "Battle flow phase contains an invalid feature reference: " + phase.StateId);

                        if (!string.IsNullOrWhiteSpace(reference.FeatureId))
                        {
                            AddFeature(reference.FeatureId, phase.StateId, features, seenFeatures);
                            continue;
                        }

                        if (!groups.TryGetValue(reference.GroupId, out var groupFeatures))
                            throw new InvalidOperationException(
                                $"Battle flow phase '{phase.StateId}' references unknown group: {reference.GroupId}");
                        foreach (var featureId in groupFeatures)
                            AddFeature(featureId, phase.StateId, features, seenFeatures);
                    }
                }

                resolved.Add(phase.StateId, Array.AsReadOnly(features.ToArray()));
                var retained = phase.RetainOnExitFeatureIds ?? Array.Empty<string>();
                if (Version < 3 && phase.StateId == "Battle.Prepare" && retained.Length == 0)
                    retained = features.ToArray();
                ValidateFeatureIds(retained, "retained features in phase '" + phase.StateId + "'");
                foreach (var featureId in retained)
                    if (!seenFeatures.Contains(featureId))
                        throw new InvalidOperationException(
                            $"Battle flow phase '{phase.StateId}' retains a feature it does not install: {featureId}");
                retainedByState.Add(phase.StateId, Array.AsReadOnly((string[])retained.Clone()));
                var enterActions = phase.EnterAfterActionIds ?? Array.Empty<string>();
                var exitActions = phase.ExitActionIds ?? Array.Empty<string>();
                if (Version < 4 && (enterActions.Length != 0 || exitActions.Length != 0))
                    throw new InvalidOperationException("Battle flow extension actions require profile version 4.");
                ValidateFeatureIds(enterActions, "enter actions in phase '" + phase.StateId + "'");
                ValidateFeatureIds(exitActions, "exit actions in phase '" + phase.StateId + "'");
                enterActionsByState.Add(phase.StateId, Array.AsReadOnly((string[])enterActions.Clone()));
                exitActionsByState.Add(phase.StateId, Array.AsReadOnly((string[])exitActions.Clone()));
            }

            foreach (var state in topology.States)
            {
                var stateId = state.StateId;
                if (!resolved.ContainsKey(stateId))
                    throw new InvalidOperationException("Battle flow phase is missing: " + stateId);
            }

            var retainedAcrossPhases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in topology.States)
            {
                var stateId = state.StateId;
                if (stateId == "Battle.End") retainedAcrossPhases.Clear();
                foreach (var featureId in resolved[stateId])
                    if (retainedAcrossPhases.Contains(featureId))
                        throw new InvalidOperationException(
                            $"Battle flow phase '{stateId}' reinstalls retained feature: {featureId}");
                foreach (var featureId in retainedByState[stateId])
                    retainedAcrossPhases.Add(featureId);
            }

            if (resolved.TryGetValue("Battle.Prepare", out var prepare))
            {
                if (prepare.Count < 2 || prepare[0] != "context" || IndexOf(prepare, "session") <= 0)
                    throw new InvalidOperationException("Battle.Prepare must attach context before session.");
                var retainedPrepare = retainedByState["Battle.Prepare"];
                foreach (var featureId in new[] { "context", "entity", "session", "battle_outcome",
                                                 "gameplay_presentation", "dungeon_context", "dungeon_scene" })
                    if (IndexOf(prepare, featureId) >= 0 && IndexOf(retainedPrepare, featureId) < 0)
                        throw new InvalidOperationException(
                            "Battle.Prepare must retain battle-scoped feature: " + featureId);
            }
            if (resolved.TryGetValue("Battle.LoadAssets", out var loadAssets) &&
                IndexOf(loadAssets, "loading_screen") < 0)
                throw new InvalidOperationException("Battle.LoadAssets requires loading_screen.");
            ValidateBuiltInFeatureDependencies(resolved);

            if (Version >= 3 && (SessionModuleIds == null || SessionModuleIds.Length == 0))
                throw new InvalidOperationException("Battle flow profile requires session modules.");
            var sessionModules = SessionModuleIds == null || SessionModuleIds.Length == 0
                ? StandardSessionModules
                : SessionModuleIds;
            ValidateFeatureIds(sessionModules, "session modules");
            var stateOrder = new string[topology.States.Count];
            for (var i = 0; i < stateOrder.Length; i++) stateOrder[i] = topology.States[i].StateId;
            return new MobaBattleFlowComposition(ProfileId, Array.AsReadOnly(stateOrder),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(resolved),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(retainedByState),
                Array.AsReadOnly((string[])sessionModules.Clone()),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(enterActionsByState),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(exitActionsByState), topology);
        }

        private static void ValidateBuiltInFeatureDependencies(
            IReadOnlyDictionary<string, IReadOnlyList<string>> features)
        {
            if (!features.TryGetValue("Battle.Prepare", out var prepare)) return;
            foreach (var required in new[] { "context", "entity", "session" })
                if (IndexOf(prepare, required) < 0)
                    throw new InvalidOperationException("Battle.Prepare requires feature: " + required);
            if (IndexOf(prepare, "battle_outcome") < IndexOf(prepare, "session"))
                throw new InvalidOperationException("Battle.Prepare requires battle_outcome after session.");
            if (features.TryGetValue("Battle.End", out var end) &&
                IndexOf(end, "end_settlement") >= 0 &&
                IndexOf(prepare, "gameplay_presentation") < 0)
                throw new InvalidOperationException("Battle.End settlement requires gameplay_presentation.");
            if (IndexOf(prepare, "entity") > IndexOf(prepare, "session"))
                throw new InvalidOperationException("Battle.Prepare requires entity before session.");
            if (!features.TryGetValue("Battle.InMatch", out var inMatch)) return;
            if (IndexOf(inMatch, "sync") >= 0)
                foreach (var featureId in new[] { "input", "view", "hud" })
                    if (IndexOf(inMatch, featureId) >= 0 &&
                        IndexOf(inMatch, "sync") > IndexOf(inMatch, featureId))
                        throw new InvalidOperationException("Battle.InMatch requires sync before " + featureId);
        }

        private MobaBattleFlowTopology ResolveTopology()
        {
            var topologyId = string.IsNullOrWhiteSpace(TopologyId) ? FixedTopologyId : TopologyId;
            var configuredStates = TopologyStates ?? Array.Empty<MobaBattleFlowTopologyState>();
            var configuredTransitions = TopologyTransitions ?? Array.Empty<MobaBattleFlowTopologyTransition>();
            if (configuredStates.Length == 0 && configuredTransitions.Length == 0)
            {
                if (!string.Equals(topologyId, FixedTopologyId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Battle flow profile uses an unsupported topology without state definitions: " + topologyId);
                configuredStates = CreateDefaultTopologyStates();
                configuredTransitions = CreateDefaultTopologyTransitions();
            }
            else if (configuredStates.Length == 0 || configuredTransitions.Length == 0)
            {
                throw new InvalidOperationException("Battle flow topology must define both states and transitions.");
            }

            var states = new List<MobaBattleFlowTopologyState>(configuredStates.Length);
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in configuredStates)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.StateId) ||
                    !stateIds.Add(source.StateId) || !TryParseBattleState(source.StateId, out _))
                    throw new InvalidOperationException("Battle flow topology contains an invalid or duplicate state: " + source?.StateId);
                states.Add(new MobaBattleFlowTopologyState
                {
                    StateId = source.StateId,
                    Description = string.IsNullOrWhiteSpace(source.Description)
                        ? source.StateId.Substring("Battle.".Length)
                        : source.Description
                });
            }

            var startStateId = string.IsNullOrWhiteSpace(TopologyStartStateId)
                ? states[0].StateId
                : TopologyStartStateId;
            if (!stateIds.Contains(startStateId))
                throw new InvalidOperationException("Battle flow topology start state is not defined: " + startStateId);

            var transitions = new List<MobaBattleFlowTopologyTransition>(configuredTransitions.Length);
            var transitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in configuredTransitions)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.EventId) ||
                    !stateIds.Contains(source.FromStateId) || !stateIds.Contains(source.ToStateId) ||
                    source.FromStateId == source.ToStateId || !TryParseBattleEvent(source.EventId, out _) ||
                    !transitionIds.Add(source.EventId + "|" + source.FromStateId + "|" + source.ToStateId))
                    throw new InvalidOperationException("Battle flow topology contains an invalid transition.");
                transitions.Add(new MobaBattleFlowTopologyTransition
                {
                    EventId = source.EventId,
                    FromStateId = source.FromStateId,
                    ToStateId = source.ToStateId
                });
            }

            return new MobaBattleFlowTopology(startStateId,
                Array.AsReadOnly(states.ToArray()), Array.AsReadOnly(transitions.ToArray()));
        }

        internal static bool TryParseBattleState(string stateId, out MobaBattleState state)
        {
            state = default;
            const string prefix = "Battle.";
            if (string.IsNullOrWhiteSpace(stateId) || !stateId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            return Enum.TryParse(stateId.Substring(prefix.Length), ignoreCase: false, out state) &&
                   Enum.IsDefined(typeof(MobaBattleState), state);
        }

        internal static bool TryParseBattleEvent(string eventId, out MobaBattleEvent battleEvent)
        {
            return Enum.TryParse(eventId, ignoreCase: false, out battleEvent) &&
                   Enum.IsDefined(typeof(MobaBattleEvent), battleEvent);
        }

        private static bool ContainsState(IReadOnlyList<MobaBattleFlowTopologyState> states, string stateId)
        {
            for (var i = 0; i < states.Count; i++)
                if (string.Equals(states[i].StateId, stateId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static MobaBattleFlowTopologyState[] CreateDefaultTopologyStates() => new[]
        {
            new MobaBattleFlowTopologyState { StateId = "Battle.Prepare", Description = "Prepare battle session" },
            new MobaBattleFlowTopologyState { StateId = "Battle.Connect", Description = "Connect battle session" },
            new MobaBattleFlowTopologyState { StateId = "Battle.CreateOrJoinWorld", Description = "Create or join battle world" },
            new MobaBattleFlowTopologyState { StateId = "Battle.LoadAssets", Description = "Load battle assets" },
            new MobaBattleFlowTopologyState { StateId = "Battle.InMatch", Description = "Run battle match" },
            new MobaBattleFlowTopologyState { StateId = "Battle.End", Description = "Finalize battle session" }
        };

        private static MobaBattleFlowTopologyTransition[] CreateDefaultTopologyTransitions() => new[]
        {
            Transition("PrepareDone", "Battle.Prepare", "Battle.Connect"),
            Transition("Connected", "Battle.Connect", "Battle.CreateOrJoinWorld"),
            Transition("JoinedWorld", "Battle.CreateOrJoinWorld", "Battle.LoadAssets"),
            Transition("AssetsLoadCompleted", "Battle.LoadAssets", "Battle.InMatch"),
            Transition("Ended", "Battle.Prepare", "Battle.End"),
            Transition("Ended", "Battle.Connect", "Battle.End"),
            Transition("Ended", "Battle.CreateOrJoinWorld", "Battle.End"),
            Transition("Ended", "Battle.LoadAssets", "Battle.End"),
            Transition("Ended", "Battle.InMatch", "Battle.End")
        };

        private static MobaBattleFlowTopologyTransition Transition(string eventId, string from, string to) =>
            new MobaBattleFlowTopologyTransition { EventId = eventId, FromStateId = from, ToStateId = to };

        public static MobaBattleFlowProfile CreateStandard() => new MobaBattleFlowProfile
        {
            ProfileId = "moba.standard",
            Version = 3,
            SessionModuleIds = (string[])StandardSessionModules.Clone(),
            Groups = new[]
            {
                new MobaBattleFlowFeatureGroup
                {
                    GroupId = "debug_overlay", FeatureIds = new[] { "debug_ongui" }
                }
            },
            Phases = new[]
            {
                new MobaBattleFlowPhase
                {
                    StateId = "Battle.Prepare",
                    FeatureIds = new[] { "context", "entity", "session", "battle_outcome",
                        "gameplay_presentation", "dungeon_context", "dungeon_scene" },
                    RetainOnExitFeatureIds = new[] { "context", "entity", "session", "battle_outcome",
                        "gameplay_presentation", "dungeon_context", "dungeon_scene" }
                },
                PhaseWithRefs("Battle.Connect", GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.CreateOrJoinWorld", GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.LoadAssets", FeatureRef("loading_screen"), GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.InMatch", FeatureRef("sync"), FeatureRef("input"),
                    FeatureRef("view"), FeatureRef("hud"), GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.End", FeatureRef("end_settlement"), GroupRef("debug_overlay"))
            }
        };

        public static MobaBattleFlowProfile CreateWithoutBattleView()
        {
            var profile = CreateStandard();
            profile.ProfileId = "moba.no-battle-view";
            profile.Groups = Array.Empty<MobaBattleFlowFeatureGroup>();
            profile.Phases[0].FeatureIds = new[]
            {
                "context", "entity", "session", "battle_outcome", "dungeon_context", "dungeon_scene"
            };
            profile.Phases[0].RetainOnExitFeatureIds =
                (string[])profile.Phases[0].FeatureIds.Clone();
            profile.Phases[1].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
            profile.Phases[2].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
            profile.Phases[3].FeatureRefs = new[] { FeatureRef("loading_screen") };
            profile.Phases[4].FeatureRefs = new[] { FeatureRef("sync") };
            profile.Phases[5].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
            return profile;
        }

        private static void ValidateFeatureIds(string[] featureIds, string owner)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var featureId in featureIds)
                if (string.IsNullOrWhiteSpace(featureId) || !seen.Add(featureId))
                    throw new InvalidOperationException(
                        $"Battle flow {owner} contains an invalid or duplicate feature: {featureId}");
        }

        private static void AddFeature(string featureId, string stateId,
            List<string> features, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(featureId) || !seen.Add(featureId))
                throw new InvalidOperationException(
                    $"Battle flow phase '{stateId}' contains an invalid or duplicate feature: {featureId}");
            features.Add(featureId);
        }

        private static int IndexOf(IReadOnlyList<string> values, string expected)
        {
            for (var i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static MobaBattleFlowPhase Phase(string stateId, params string[] featureIds) =>
            new MobaBattleFlowPhase { StateId = stateId, FeatureIds = featureIds };

        private static MobaBattleFlowPhase PhaseWithRefs(string stateId,
            params MobaBattleFlowFeatureRef[] featureRefs) =>
            new MobaBattleFlowPhase { StateId = stateId, FeatureRefs = featureRefs };

        private static MobaBattleFlowFeatureRef FeatureRef(string featureId) =>
            new MobaBattleFlowFeatureRef { FeatureId = featureId };

        private static MobaBattleFlowFeatureRef GroupRef(string groupId) =>
            new MobaBattleFlowFeatureRef { GroupId = groupId };
    }
}

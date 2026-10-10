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
            IReadOnlyDictionary<string, IReadOnlyList<string>> exitActionsByState)
        {
            ProfileId = profileId;
            StateOrder = stateOrder;
            _featuresByState = featuresByState;
            _retainedFeaturesByState = retainedFeaturesByState;
            SessionModuleIds = sessionModuleIds;
            _enterActionsByState = enterActionsByState;
            _exitActionsByState = exitActionsByState;
        }

        public string ProfileId { get; }
        public IReadOnlyList<string> StateOrder { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> FeaturesByState => _featuresByState;
        public IReadOnlyList<string> SessionModuleIds { get; }
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
    }

    [Serializable]
    public sealed class MobaBattleFlowProfile
    {
        public const string FixedTopologyId = "moba.root-battle.v1";
        private static readonly string[] RequiredPhases =
        {
            "Battle.Prepare", "Battle.Connect", "Battle.CreateOrJoinWorld",
            "Battle.LoadAssets", "Battle.InMatch", "Battle.End"
        };
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
            if (!string.IsNullOrWhiteSpace(TopologyId) &&
                !string.Equals(TopologyId, FixedTopologyId, StringComparison.Ordinal))
                throw new InvalidOperationException("Battle flow profile uses unsupported topology: " + TopologyId);
            if (string.IsNullOrWhiteSpace(ProfileId) || (Version < 1 || Version > 4) ||
                Phases == null || Phases.Length != RequiredPhases.Length)
                throw new InvalidOperationException("Battle flow profile header or phase count is invalid.");
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
                    Array.IndexOf(RequiredPhases, phase.StateId) < 0 ||
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

            foreach (var stateId in RequiredPhases)
                if (!resolved.ContainsKey(stateId))
                    throw new InvalidOperationException("Battle flow phase is missing: " + stateId);

            var retainedAcrossPhases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stateId in RequiredPhases)
            {
                if (stateId == "Battle.End") retainedAcrossPhases.Clear();
                foreach (var featureId in resolved[stateId])
                    if (retainedAcrossPhases.Contains(featureId))
                        throw new InvalidOperationException(
                            $"Battle flow phase '{stateId}' reinstalls retained feature: {featureId}");
                foreach (var featureId in retainedByState[stateId])
                    retainedAcrossPhases.Add(featureId);
            }

            var prepare = resolved["Battle.Prepare"];
            if (prepare.Count < 2 || prepare[0] != "context" || IndexOf(prepare, "session") <= 0)
                throw new InvalidOperationException("Battle.Prepare must attach context before session.");
            var retainedPrepare = retainedByState["Battle.Prepare"];
            foreach (var featureId in new[] { "context", "entity", "session", "battle_outcome", "gameplay_presentation" })
                if (IndexOf(prepare, featureId) >= 0 && IndexOf(retainedPrepare, featureId) < 0)
                    throw new InvalidOperationException(
                        "Battle.Prepare must retain battle-scoped feature: " + featureId);
            if (IndexOf(resolved["Battle.LoadAssets"], "loading_screen") < 0)
                throw new InvalidOperationException("Battle.LoadAssets requires loading_screen.");
            ValidateBuiltInFeatureDependencies(resolved);

            if (Version >= 3 && (SessionModuleIds == null || SessionModuleIds.Length == 0))
                throw new InvalidOperationException("Battle flow profile requires session modules.");
            var sessionModules = SessionModuleIds == null || SessionModuleIds.Length == 0
                ? StandardSessionModules
                : SessionModuleIds;
            ValidateFeatureIds(sessionModules, "session modules");
            return new MobaBattleFlowComposition(ProfileId, Array.AsReadOnly((string[])RequiredPhases.Clone()),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(resolved),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(retainedByState),
                Array.AsReadOnly((string[])sessionModules.Clone()),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(enterActionsByState),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(exitActionsByState));
        }

        private static void ValidateBuiltInFeatureDependencies(
            IReadOnlyDictionary<string, IReadOnlyList<string>> features)
        {
            var prepare = features["Battle.Prepare"];
            foreach (var required in new[] { "context", "entity", "session" })
                if (IndexOf(prepare, required) < 0)
                    throw new InvalidOperationException("Battle.Prepare requires feature: " + required);
            if (IndexOf(prepare, "battle_outcome") < IndexOf(prepare, "session"))
                throw new InvalidOperationException("Battle.Prepare requires battle_outcome after session.");
            if (IndexOf(features["Battle.End"], "end_settlement") >= 0 &&
                IndexOf(prepare, "gameplay_presentation") < 0)
                throw new InvalidOperationException("Battle.End settlement requires gameplay_presentation.");
            if (IndexOf(prepare, "entity") > IndexOf(prepare, "session"))
                throw new InvalidOperationException("Battle.Prepare requires entity before session.");
            var inMatch = features["Battle.InMatch"];
            if (IndexOf(inMatch, "sync") >= 0)
                foreach (var featureId in new[] { "input", "view", "hud" })
                    if (IndexOf(inMatch, featureId) >= 0 &&
                        IndexOf(inMatch, "sync") > IndexOf(inMatch, featureId))
                        throw new InvalidOperationException("Battle.InMatch requires sync before " + featureId);
        }

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
                    FeatureIds = new[] { "context", "entity", "session", "battle_outcome", "gameplay_presentation" },
                    RetainOnExitFeatureIds = new[] { "context", "entity", "session", "battle_outcome", "gameplay_presentation" }
                },
                PhaseWithRefs("Battle.Connect", GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.CreateOrJoinWorld", GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.LoadAssets", FeatureRef("loading_screen"), GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.InMatch", FeatureRef("sync"), FeatureRef("input"),
                    FeatureRef("view"), FeatureRef("hud"), GroupRef("debug_overlay")),
                PhaseWithRefs("Battle.End", FeatureRef("end_settlement"), GroupRef("debug_overlay"))
            }
        };

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

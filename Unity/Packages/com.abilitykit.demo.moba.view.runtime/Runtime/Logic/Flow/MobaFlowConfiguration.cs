using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaFlowConfiguration
    {
        private MobaFlowConfiguration(
            PhaseStateMachineSpec<MobaRootState, MobaRootEvent> rootMachine,
            PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent> battleMachine,
            PhaseStateFeatureSpec bootFeatures,
            PhaseStateFeatureSpec lobbyFeatures,
            PhaseStateFeatureSpec battlePrepareFeatures,
            PhaseStateFeatureSpec battleConnectFeatures,
            PhaseStateFeatureSpec battleCreateOrJoinWorldFeatures,
            PhaseStateFeatureSpec battleLoadAssetsFeatures,
            PhaseStateFeatureSpec battleInMatchFeatures,
            PhaseStateFeatureSpec battleEndFeatures,
            string battleProfileId,
            IReadOnlyDictionary<MobaBattleState, IReadOnlyList<string>> battleRetainedFeatureIds,
            IReadOnlyDictionary<MobaRootState, string> rootStateDescriptions,
            IReadOnlyDictionary<MobaBattleState, string> battleStateDescriptions,
            MobaBattleFlowTopology battleTopology)
        {
            RootMachine = rootMachine ?? throw new ArgumentNullException(nameof(rootMachine));
            BattleMachine = battleMachine ?? throw new ArgumentNullException(nameof(battleMachine));
            BootFeatures = bootFeatures ?? throw new ArgumentNullException(nameof(bootFeatures));
            LobbyFeatures = lobbyFeatures ?? throw new ArgumentNullException(nameof(lobbyFeatures));
            BattlePrepareFeatures = battlePrepareFeatures ?? throw new ArgumentNullException(nameof(battlePrepareFeatures));
            BattleConnectFeatures = battleConnectFeatures ?? throw new ArgumentNullException(nameof(battleConnectFeatures));
            BattleCreateOrJoinWorldFeatures = battleCreateOrJoinWorldFeatures ?? throw new ArgumentNullException(nameof(battleCreateOrJoinWorldFeatures));
            BattleLoadAssetsFeatures = battleLoadAssetsFeatures ?? throw new ArgumentNullException(nameof(battleLoadAssetsFeatures));
            BattleInMatchFeatures = battleInMatchFeatures ?? throw new ArgumentNullException(nameof(battleInMatchFeatures));
            BattleEndFeatures = battleEndFeatures ?? throw new ArgumentNullException(nameof(battleEndFeatures));
            BattleFeatureSpecs = new Dictionary<MobaBattleState, PhaseStateFeatureSpec>
            {
                [MobaBattleState.Prepare] = BattlePrepareFeatures,
                [MobaBattleState.Connect] = BattleConnectFeatures,
                [MobaBattleState.CreateOrJoinWorld] = BattleCreateOrJoinWorldFeatures,
                [MobaBattleState.LoadAssets] = BattleLoadAssetsFeatures,
                [MobaBattleState.InMatch] = BattleInMatchFeatures,
                [MobaBattleState.End] = BattleEndFeatures
            };
            BattleProfileId = battleProfileId ?? throw new ArgumentNullException(nameof(battleProfileId));
            BattleRetainedFeatureIds = battleRetainedFeatureIds ?? throw new ArgumentNullException(nameof(battleRetainedFeatureIds));
            RootStateDescriptions = rootStateDescriptions ?? throw new ArgumentNullException(nameof(rootStateDescriptions));
            BattleStateDescriptions = battleStateDescriptions ?? throw new ArgumentNullException(nameof(battleStateDescriptions));
            BattleTopology = battleTopology ?? throw new ArgumentNullException(nameof(battleTopology));
        }

        public PhaseStateMachineSpec<MobaRootState, MobaRootEvent> RootMachine { get; }
        public PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent> BattleMachine { get; }
        public PhaseStateFeatureSpec BootFeatures { get; }
        public PhaseStateFeatureSpec LobbyFeatures { get; }
        public PhaseStateFeatureSpec BattlePrepareFeatures { get; }
        public PhaseStateFeatureSpec BattleConnectFeatures { get; }
        public PhaseStateFeatureSpec BattleCreateOrJoinWorldFeatures { get; }
        public PhaseStateFeatureSpec BattleLoadAssetsFeatures { get; }
        public PhaseStateFeatureSpec BattleInMatchFeatures { get; }
        public PhaseStateFeatureSpec BattleEndFeatures { get; }
        public IReadOnlyDictionary<MobaBattleState, PhaseStateFeatureSpec> BattleFeatureSpecs { get; }
        public string BattleProfileId { get; }
        public IReadOnlyDictionary<MobaBattleState, IReadOnlyList<string>> BattleRetainedFeatureIds { get; }
        public IReadOnlyDictionary<MobaRootState, string> RootStateDescriptions { get; }
        public IReadOnlyDictionary<MobaBattleState, string> BattleStateDescriptions { get; }
        public MobaBattleFlowTopology BattleTopology { get; }

        public string GetRootStateDescription(MobaRootState state) => RootStateDescriptions[state];
        public string GetBattleStateDescription(MobaBattleState state) => BattleStateDescriptions[state];

        public static MobaFlowConfiguration CreateDefault(MobaBattleFlowProfile profile = null) =>
            CreateResolved((profile ?? MobaBattleFlowProfile.CreateStandard()).Resolve());

        internal static MobaFlowConfiguration CreateResolved(MobaBattleFlowComposition composition)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            var configuration = new MobaFlowConfiguration(
                BuildRootMachine(),
                BuildBattleMachine(composition),
                new PhaseStateFeatureSpec("Boot", clearBeforeEnter: true),
                new PhaseStateFeatureSpec("Lobby", clearBeforeEnter: true)
                    .AddFeature("demo_lobby")
                    .AddFeature("formal_lobby")
                    .AddFeature("formal_lobby_ui"),
                BuildBattlePhaseSpec(MobaBattleState.Prepare, composition),
                BuildBattlePhaseSpec(MobaBattleState.Connect, composition),
                BuildBattlePhaseSpec(MobaBattleState.CreateOrJoinWorld, composition),
                BuildBattlePhaseSpec(MobaBattleState.LoadAssets, composition),
                BuildBattlePhaseSpec(MobaBattleState.InMatch, composition),
                BuildBattlePhaseSpec(MobaBattleState.End, composition),
                composition.ProfileId,
                BuildRetainedFeatureIds(composition),
                BuildRootStateDescriptions(),
                BuildBattleStateDescriptions(composition),
                composition.Topology);

            MobaFlowConfigurationValidator.ValidateOrThrow(configuration);
            return configuration;
        }

        private static PhaseStateFeatureSpec AddFeatures(
            PhaseStateFeatureSpec spec, MobaBattleFlowComposition composition)
        {
            foreach (var featureId in composition.GetFeatures(spec.StateId)) spec.AddFeature(featureId);
            foreach (var actionId in composition.GetEnterActions(spec.StateId)) spec.AddEnterAfterAction(actionId);
            foreach (var actionId in composition.GetExitActions(spec.StateId)) spec.AddExitAction(actionId);
            return spec;
        }

        private static PhaseStateFeatureSpec BuildBattlePhaseSpec(
            MobaBattleState state, MobaBattleFlowComposition composition)
        {
            var spec = new PhaseStateFeatureSpec(
                "Battle." + state,
                clearBeforeEnter: state == MobaBattleState.Prepare || state == MobaBattleState.End);
            if (state == MobaBattleState.Prepare)
                spec.AddEnterBeforeAction(MobaFlowActionIds.ResetBattleSessionRuntimeState);
            if (composition.FeaturesByState.ContainsKey(spec.StateId))
                AddFeatures(spec, composition);
            if (state == MobaBattleState.Connect)
                spec.AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnConnectEnter);
            else if (state == MobaBattleState.CreateOrJoinWorld)
                spec.AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnCreateOrJoinWorldEnter);
            else if (state == MobaBattleState.LoadAssets)
                spec.AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnLoadAssetsEnter);
            else if (state == MobaBattleState.End)
                spec.AddEnterAfterAction(MobaFlowActionIds.ReturnLobbyAfterBattleEnd);
            return spec;
        }

        private static IReadOnlyDictionary<MobaBattleState, IReadOnlyList<string>> BuildRetainedFeatureIds(
            MobaBattleFlowComposition composition)
        {
            var result = new Dictionary<MobaBattleState, IReadOnlyList<string>>();
            foreach (MobaBattleState state in Enum.GetValues(typeof(MobaBattleState)))
                result[state] = composition.FeaturesByState.ContainsKey("Battle." + state)
                    ? composition.GetRetainedFeatures("Battle." + state)
                    : Array.Empty<string>();
            return result;
        }

        private static IReadOnlyDictionary<MobaRootState, string> BuildRootStateDescriptions()
        {
            return new Dictionary<MobaRootState, string>
            {
                [MobaRootState.Boot] = "Boot",
                [MobaRootState.Lobby] = "Lobby",
                [MobaRootState.Battle] = "Battle",
            };
        }

        private static IReadOnlyDictionary<MobaBattleState, string> BuildBattleStateDescriptions(
            MobaBattleFlowComposition composition)
        {
            var descriptions = new Dictionary<MobaBattleState, string>
            {
                [MobaBattleState.Prepare] = "Prepare battle session",
                [MobaBattleState.Connect] = "Connect battle session",
                [MobaBattleState.CreateOrJoinWorld] = "Create or join battle world",
                [MobaBattleState.LoadAssets] = "Load battle assets",
                [MobaBattleState.InMatch] = "Run battle match",
                [MobaBattleState.End] = "Finalize battle session",
            };
            foreach (var state in composition.Topology.States)
                if (MobaBattleFlowProfile.TryParseBattleState(state.StateId, out var battleState))
                    descriptions[battleState] = state.Description;
            return descriptions;
        }

        private static PhaseStateMachineSpec<MobaRootState, MobaRootEvent> BuildRootMachine()
        {
            return new PhaseStateMachineSpec<MobaRootState, MobaRootEvent>("Root", 3, 5)
                .AddState(MobaRootState.Boot)
                .AddState(MobaRootState.Lobby)
                .AddState(MobaRootState.Battle)
                .SetStartState(MobaRootState.Boot)
                .AddTransition(MobaRootEvent.BootCompleted, MobaRootState.Boot, MobaRootState.Lobby)
                .AddTransition(MobaRootEvent.EnterBattle, MobaRootState.Lobby, MobaRootState.Battle, MobaFlowConditionIds.BattleEntryReady)
                .AddTransition(MobaRootEvent.EnterBattle, MobaRootState.Boot, MobaRootState.Battle, MobaFlowConditionIds.BattleEntryReady)
                .AddTransition(MobaRootEvent.ReturnLobby, MobaRootState.Battle, MobaRootState.Lobby)
                .AddTransition(MobaRootEvent.ReturnLobby, MobaRootState.Boot, MobaRootState.Lobby);
        }

        private static PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent> BuildBattleMachine()
        {
            return BuildBattleMachine(MobaBattleFlowProfile.CreateStandard().Resolve());
                // 真实资源加载完成（manifest barrier）驱动 LoadAssets → InMatch。
        }

        private static PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent> BuildBattleMachine(
            MobaBattleFlowComposition composition)
        {
            var machine = new PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent>(
                "Battle", composition.Topology.States.Count, composition.Topology.Transitions.Count);
            foreach (var state in composition.Topology.States)
            {
                if (!MobaBattleFlowProfile.TryParseBattleState(state.StateId, out var battleState))
                    throw new InvalidOperationException("Battle topology state cannot map to MobaBattleState: " + state.StateId);
                machine.AddState(battleState);
            }
            if (!MobaBattleFlowProfile.TryParseBattleState(composition.Topology.StartStateId, out var startState))
                throw new InvalidOperationException("Battle topology start state cannot be mapped.");
            machine.SetStartState(startState);
            foreach (var transition in composition.Topology.Transitions)
            {
                if (!MobaBattleFlowProfile.TryParseBattleEvent(transition.EventId, out var battleEvent) ||
                    !MobaBattleFlowProfile.TryParseBattleState(transition.FromStateId, out var from) ||
                    !MobaBattleFlowProfile.TryParseBattleState(transition.ToStateId, out var to))
                    throw new InvalidOperationException("Battle topology transition cannot be mapped.");
                machine.AddTransition(battleEvent, from, to);
            }
            return machine;
        }
    }
}

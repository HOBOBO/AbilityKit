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
            IReadOnlyDictionary<MobaRootState, string> rootStateDescriptions,
            IReadOnlyDictionary<MobaBattleState, string> battleStateDescriptions)
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
            RootStateDescriptions = rootStateDescriptions ?? throw new ArgumentNullException(nameof(rootStateDescriptions));
            BattleStateDescriptions = battleStateDescriptions ?? throw new ArgumentNullException(nameof(battleStateDescriptions));
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
        public IReadOnlyDictionary<MobaRootState, string> RootStateDescriptions { get; }
        public IReadOnlyDictionary<MobaBattleState, string> BattleStateDescriptions { get; }

        public string GetRootStateDescription(MobaRootState state) => RootStateDescriptions[state];
        public string GetBattleStateDescription(MobaBattleState state) => BattleStateDescriptions[state];

        public static MobaFlowConfiguration CreateDefault()
        {
            var configuration = new MobaFlowConfiguration(
                BuildRootMachine(),
                BuildBattleMachine(),
                new PhaseStateFeatureSpec("Boot", clearBeforeEnter: true),
                new PhaseStateFeatureSpec("Lobby", clearBeforeEnter: true)
                    .AddFeature("demo_lobby")
                    .AddFeature("formal_lobby"),
                new PhaseStateFeatureSpec("Battle.Prepare", clearBeforeEnter: true)
                    .AddEnterBeforeAction(MobaFlowActionIds.ResetBattleSessionRuntimeState)
                    .AddFeature("context")
                    .AddFeature("entity")
                    .AddFeature("session"),
                new PhaseStateFeatureSpec("Battle.Connect")
                    .AddFeature("debug_ongui")
                    .AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnConnectEnter),
                new PhaseStateFeatureSpec("Battle.CreateOrJoinWorld")
                    .AddFeature("debug_ongui")
                    .AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnCreateOrJoinWorldEnter),
                new PhaseStateFeatureSpec("Battle.LoadAssets")
                    .AddFeature("loading_screen")
                    .AddFeature("debug_ongui")
                    .AddSwitchFlow(MobaFlowSwitchIds.AdvanceOnLoadAssetsEnter),
                new PhaseStateFeatureSpec("Battle.InMatch")
                    .AddFeature("sync")
                    .AddFeature("input")
                    .AddFeature("view")
                    .AddFeature("hud")
                    .AddFeature("end_recorder")
                    .AddFeature("debug_ongui"),
                new PhaseStateFeatureSpec("Battle.End", clearBeforeEnter: true)
                    .AddFeature("end_settlement")
                    .AddFeature("debug_ongui")
                    .AddEnterAfterAction(MobaFlowActionIds.ReturnLobbyAfterBattleEnd),
                BuildRootStateDescriptions(),
                BuildBattleStateDescriptions());

            MobaFlowConfigurationValidator.ValidateOrThrow(configuration);
            return configuration;
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

        private static IReadOnlyDictionary<MobaBattleState, string> BuildBattleStateDescriptions()
        {
            return new Dictionary<MobaBattleState, string>
            {
                [MobaBattleState.Prepare] = "Prepare battle session",
                [MobaBattleState.Connect] = "Connect battle session",
                [MobaBattleState.CreateOrJoinWorld] = "Create or join battle world",
                [MobaBattleState.LoadAssets] = "Load battle assets",
                [MobaBattleState.InMatch] = "Run battle match",
                [MobaBattleState.End] = "Finalize battle session",
            };
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
            return new PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent>("Battle", 6, 9)
                .AddState(MobaBattleState.Prepare)
                .AddState(MobaBattleState.Connect)
                .AddState(MobaBattleState.CreateOrJoinWorld)
                .AddState(MobaBattleState.LoadAssets)
                .AddState(MobaBattleState.InMatch)
                .AddState(MobaBattleState.End)
                .SetStartState(MobaBattleState.Prepare)
                .AddTransition(MobaBattleEvent.PrepareDone, MobaBattleState.Prepare, MobaBattleState.Connect)
                .AddTransition(MobaBattleEvent.Connected, MobaBattleState.Connect, MobaBattleState.CreateOrJoinWorld)
                .AddTransition(MobaBattleEvent.JoinedWorld, MobaBattleState.CreateOrJoinWorld, MobaBattleState.LoadAssets)
                // 真实资源加载完成（manifest barrier）驱动 LoadAssets → InMatch。
                .AddTransition(MobaBattleEvent.AssetsLoadCompleted, MobaBattleState.LoadAssets, MobaBattleState.InMatch)
                .AddTransition(MobaBattleEvent.Ended, MobaBattleState.Prepare, MobaBattleState.End)
                .AddTransition(MobaBattleEvent.Ended, MobaBattleState.Connect, MobaBattleState.End)
                .AddTransition(MobaBattleEvent.Ended, MobaBattleState.CreateOrJoinWorld, MobaBattleState.End)
                .AddTransition(MobaBattleEvent.Ended, MobaBattleState.LoadAssets, MobaBattleState.End)
                .AddTransition(MobaBattleEvent.Ended, MobaBattleState.InMatch, MobaBattleState.End);
        }
    }
}

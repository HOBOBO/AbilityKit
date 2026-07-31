using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests
{
    /// <summary>
    /// MobaFlowConfiguration.CreateDefault() 的特征化测试。
    /// 不改产品代码，只锁死流程拓扑（状态机 + 迁移 + 条件 + 每态 feature/action/switch 列表），
    /// 防止未来意外改动破坏隐式契约。
    /// </summary>
    public sealed class MobaFlowConfigurationTests
    {
        private readonly MobaFlowConfiguration _config = MobaFlowConfiguration.CreateDefault();

        // ====================================================================
        // Root 状态机拓扑
        // ====================================================================

        [Fact]
        public void RootMachine_Id_IsRoot()
        {
            Assert.Equal("Root", _config.RootMachine.Id);
        }

        [Fact]
        public void RootMachine_Has3States_BootLobbyBattle()
        {
            var states = _config.RootMachine.States;
            Assert.Equal(3, states.Count);
            Assert.Equal(MobaRootState.Boot, states[0]);
            Assert.Equal(MobaRootState.Lobby, states[1]);
            Assert.Equal(MobaRootState.Battle, states[2]);
        }

        [Fact]
        public void RootMachine_StartState_IsBoot()
        {
            Assert.True(_config.RootMachine.HasStartState);
            Assert.Equal(MobaRootState.Boot, _config.RootMachine.StartState);
        }

        [Fact]
        public void RootMachine_Has5Transitions()
        {
            Assert.Equal(5, _config.RootMachine.Transitions.Count);
        }

        [Fact]
        public void RootMachine_BootCompleted_BootToLobby()
        {
            var t = _config.RootMachine.Transitions[0];
            Assert.Equal(MobaRootEvent.BootCompleted, t.Trigger);
            Assert.Equal(MobaRootState.Boot, t.From);
            Assert.Equal(MobaRootState.Lobby, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void RootMachine_EnterBattle_LobbyToBattle_GatedByBattleEntryReady()
        {
            var t = _config.RootMachine.Transitions[1];
            Assert.Equal(MobaRootEvent.EnterBattle, t.Trigger);
            Assert.Equal(MobaRootState.Lobby, t.From);
            Assert.Equal(MobaRootState.Battle, t.To);
            Assert.Equal(MobaFlowConditionIds.BattleEntryReady, t.ConditionId);
        }

        [Fact]
        public void RootMachine_EnterBattle_BootToBattle_GatedByBattleEntryReady()
        {
            var t = _config.RootMachine.Transitions[2];
            Assert.Equal(MobaRootEvent.EnterBattle, t.Trigger);
            Assert.Equal(MobaRootState.Boot, t.From);
            Assert.Equal(MobaRootState.Battle, t.To);
            Assert.Equal(MobaFlowConditionIds.BattleEntryReady, t.ConditionId);
        }

        [Fact]
        public void RootMachine_ReturnLobby_BattleToLobby()
        {
            var t = _config.RootMachine.Transitions[3];
            Assert.Equal(MobaRootEvent.ReturnLobby, t.Trigger);
            Assert.Equal(MobaRootState.Battle, t.From);
            Assert.Equal(MobaRootState.Lobby, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void RootMachine_ReturnLobby_BootToLobby()
        {
            var t = _config.RootMachine.Transitions[4];
            Assert.Equal(MobaRootEvent.ReturnLobby, t.Trigger);
            Assert.Equal(MobaRootState.Boot, t.From);
            Assert.Equal(MobaRootState.Lobby, t.To);
            Assert.Null(t.ConditionId);
        }

        // ====================================================================
        // Battle 状态机拓扑
        // ====================================================================

        [Fact]
        public void BattleMachine_Id_IsBattle()
        {
            Assert.Equal("Battle", _config.BattleMachine.Id);
        }

        [Fact]
        public void BattleMachine_Has6States_LinearChain()
        {
            var states = _config.BattleMachine.States;
            Assert.Equal(6, states.Count);
            Assert.Equal(MobaBattleState.Prepare, states[0]);
            Assert.Equal(MobaBattleState.Connect, states[1]);
            Assert.Equal(MobaBattleState.CreateOrJoinWorld, states[2]);
            Assert.Equal(MobaBattleState.LoadAssets, states[3]);
            Assert.Equal(MobaBattleState.InMatch, states[4]);
            Assert.Equal(MobaBattleState.End, states[5]);
        }

        [Fact]
        public void BattleMachine_StartState_IsPrepare()
        {
            Assert.True(_config.BattleMachine.HasStartState);
            Assert.Equal(MobaBattleState.Prepare, _config.BattleMachine.StartState);
        }

        [Fact]
        public void BattleMachine_Has10Transitions()
        {
            // 阶段 7a：追加 AssetsLoadCompleted（LoadAssets→InMatch），保留旧 LoadingDone（向后兼容）。
            Assert.Equal(10, _config.BattleMachine.Transitions.Count);
        }

        [Fact]
        public void BattleMachine_PrepareDone_PrepareToConnect()
        {
            var t = _config.BattleMachine.Transitions[0];
            Assert.Equal(MobaBattleEvent.PrepareDone, t.Trigger);
            Assert.Equal(MobaBattleState.Prepare, t.From);
            Assert.Equal(MobaBattleState.Connect, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void BattleMachine_Connected_ConnectToCreateOrJoinWorld()
        {
            var t = _config.BattleMachine.Transitions[1];
            Assert.Equal(MobaBattleEvent.Connected, t.Trigger);
            Assert.Equal(MobaBattleState.Connect, t.From);
            Assert.Equal(MobaBattleState.CreateOrJoinWorld, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void BattleMachine_JoinedWorld_CreateOrJoinWorldToLoadAssets()
        {
            var t = _config.BattleMachine.Transitions[2];
            Assert.Equal(MobaBattleEvent.JoinedWorld, t.Trigger);
            Assert.Equal(MobaBattleState.CreateOrJoinWorld, t.From);
            Assert.Equal(MobaBattleState.LoadAssets, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void BattleMachine_AssetsLoadCompleted_LoadAssetsToInMatch()
        {
            // 阶段 7a：真实资源加载完成（manifest barrier）驱动 LoadAssets → InMatch。
            var t = _config.BattleMachine.Transitions[3];
            Assert.Equal(MobaBattleEvent.AssetsLoadCompleted, t.Trigger);
            Assert.Equal(MobaBattleState.LoadAssets, t.From);
            Assert.Equal(MobaBattleState.InMatch, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void BattleMachine_LoadingDone_LoadAssetsToInMatch_BackCompat()
        {
            // 阶段 7a：旧 LoadingDone 转换保留以向后兼容（不再被触发）。
            var t = _config.BattleMachine.Transitions[4];
            Assert.Equal(MobaBattleEvent.LoadingDone, t.Trigger);
            Assert.Equal(MobaBattleState.LoadAssets, t.From);
            Assert.Equal(MobaBattleState.InMatch, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void BattleMachine_Ended_InMatchToEnd()
        {
            var t = _config.BattleMachine.Transitions[9];
            Assert.Equal(MobaBattleEvent.Ended, t.Trigger);
            Assert.Equal(MobaBattleState.InMatch, t.From);
            Assert.Equal(MobaBattleState.End, t.To);
            Assert.Null(t.ConditionId);
        }

        [Fact]
        public void StateDescriptions_AreStableAndCoverEveryConfiguredState()
        {
            foreach (MobaRootState state in Enum.GetValues(typeof(MobaRootState)))
            {
                Assert.False(string.IsNullOrWhiteSpace(_config.GetRootStateDescription(state)), state.ToString());
            }

            foreach (MobaBattleState state in Enum.GetValues(typeof(MobaBattleState)))
            {
                Assert.False(string.IsNullOrWhiteSpace(_config.GetBattleStateDescription(state)), state.ToString());
            }
        }

        [Fact]
        public void ConfigurationValidator_AcceptsDefaultConfiguration()
        {
            var result = MobaFlowConfigurationValidator.Validate(_config);

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void ConfigurationValidator_ReportsMissingStateDescription()
        {
            var descriptions = (Dictionary<MobaBattleState, string>)_config.BattleStateDescriptions;
            descriptions.Remove(MobaBattleState.LoadAssets);

            var result = MobaFlowConfigurationValidator.Validate(_config);

            Assert.False(result.IsValid);
            Assert.Contains("MOBA battle state has no stable description: LoadAssets", result.Errors);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Boot
        // ====================================================================

        [Fact]
        public void BootFeatures_StateId_ClearBeforeEnter_NoFeaturesNoActions()
        {
            var f = _config.BootFeatures;
            Assert.Equal("Boot", f.StateId);
            Assert.True(f.ClearBeforeEnter);
            Assert.Empty(f.FeatureIds);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
            Assert.Empty(f.SwitchFlowIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Lobby
        // ====================================================================

        [Fact]
        public void LobbyFeatures_StateId_ClearBeforeEnter_DemoLobbyFormalLobbyRootDebug_NoActions()
        {
            // 阶段 7b：append-only 追加 formal_lobby（不删除 demo_lobby）。
            var f = _config.LobbyFeatures;
            Assert.Equal("Lobby", f.StateId);
            Assert.True(f.ClearBeforeEnter);
            Assert.Equal(3, f.FeatureIds.Count);
            Assert.Equal("demo_lobby", f.FeatureIds[0]);
            Assert.Equal("formal_lobby", f.FeatureIds[1]);
            Assert.Equal("root_debug", f.FeatureIds[2]);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
            Assert.Empty(f.SwitchFlowIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.Prepare
        // ====================================================================

        [Fact]
        public void BattlePrepareFeatures_ClearBeforeEnter_EnterBeforeResetSession()
        {
            var f = _config.BattlePrepareFeatures;
            Assert.Equal("Battle.Prepare", f.StateId);
            Assert.True(f.ClearBeforeEnter);
            Assert.Single(f.EnterBeforeActionIds);
            Assert.Equal(MobaFlowActionIds.ResetBattleSessionRuntimeState, f.EnterBeforeActionIds[0]);
        }

        [Fact]
        public void BattlePrepareFeatures_3Features_ContextEntitySession()
        {
            var ids = _config.BattlePrepareFeatures.FeatureIds;
            Assert.Equal(3, ids.Count);
            Assert.Equal("context", ids[0]);
            Assert.Equal("entity", ids[1]);
            Assert.Equal("session", ids[2]);
        }

        [Fact]
        public void BattlePrepareFeatures_NoEnterAfter_NoExit_NoSwitchFlow()
        {
            var f = _config.BattlePrepareFeatures;
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
            Assert.Empty(f.SwitchFlowIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.Connect
        // ====================================================================

        [Fact]
        public void BattleConnectFeatures_NoClear_DebugOngui_AdvanceOnConnectEnter()
        {
            var f = _config.BattleConnectFeatures;
            Assert.Equal("Battle.Connect", f.StateId);
            Assert.False(f.ClearBeforeEnter);
            Assert.Single(f.FeatureIds);
            Assert.Equal("debug_ongui", f.FeatureIds[0]);
            Assert.Single(f.SwitchFlowIds);
            Assert.Equal(MobaFlowSwitchIds.AdvanceOnConnectEnter, f.SwitchFlowIds[0]);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.CreateOrJoinWorld
        // ====================================================================

        [Fact]
        public void BattleCreateOrJoinWorldFeatures_NoClear_DebugOngui_AdvanceOnCreateOrJoinWorldEnter()
        {
            var f = _config.BattleCreateOrJoinWorldFeatures;
            Assert.Equal("Battle.CreateOrJoinWorld", f.StateId);
            Assert.False(f.ClearBeforeEnter);
            Assert.Single(f.FeatureIds);
            Assert.Equal("debug_ongui", f.FeatureIds[0]);
            Assert.Single(f.SwitchFlowIds);
            Assert.Equal(MobaFlowSwitchIds.AdvanceOnCreateOrJoinWorldEnter, f.SwitchFlowIds[0]);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.LoadAssets
        // ====================================================================

        [Fact]
        public void BattleLoadAssetsFeatures_NoClear_LoadingScreenDebugOngui_AdvanceOnLoadAssetsEnter()
        {
            var f = _config.BattleLoadAssetsFeatures;
            Assert.Equal("Battle.LoadAssets", f.StateId);
            Assert.False(f.ClearBeforeEnter);
            Assert.Equal(2, f.FeatureIds.Count);
            Assert.Equal("loading_screen", f.FeatureIds[0]);
            Assert.Equal("debug_ongui", f.FeatureIds[1]);
            Assert.Single(f.SwitchFlowIds);
            Assert.Equal(MobaFlowSwitchIds.AdvanceOnLoadAssetsEnter, f.SwitchFlowIds[0]);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.InMatch
        // ====================================================================

        [Fact]
        public void BattleInMatchFeatures_NoClear_6Features_NoActions()
        {
            var f = _config.BattleInMatchFeatures;
            Assert.Equal("Battle.InMatch", f.StateId);
            Assert.False(f.ClearBeforeEnter);

            var ids = f.FeatureIds;
            Assert.Equal(6, ids.Count);
            Assert.Equal("sync", ids[0]);
            Assert.Equal("input", ids[1]);
            Assert.Equal("view", ids[2]);
            Assert.Equal("hud", ids[3]);
            Assert.Equal("end_recorder", ids[4]);
            Assert.Equal("debug_ongui", ids[5]);

            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.EnterAfterActionIds);
            Assert.Empty(f.ExitActionIds);
            Assert.Empty(f.SwitchFlowIds);
        }

        // ====================================================================
        // PhaseStateFeatureSpec — Battle.End
        // ====================================================================

        [Fact]
        public void BattleEndFeatures_ClearBeforeEnter_SettlementDebugOngui_EnterAfterReturnLobby()
        {
            var f = _config.BattleEndFeatures;
            Assert.Equal("Battle.End", f.StateId);
            Assert.True(f.ClearBeforeEnter);
            Assert.Equal(2, f.FeatureIds.Count);
            Assert.Equal("end_settlement", f.FeatureIds[0]);
            Assert.Equal("debug_ongui", f.FeatureIds[1]);
            Assert.Single(f.EnterAfterActionIds);
            Assert.Equal(MobaFlowActionIds.ReturnLobbyAfterBattleEnd, f.EnterAfterActionIds[0]);
            Assert.Empty(f.EnterBeforeActionIds);
            Assert.Empty(f.ExitActionIds);
            Assert.Empty(f.SwitchFlowIds);
        }
    }
}

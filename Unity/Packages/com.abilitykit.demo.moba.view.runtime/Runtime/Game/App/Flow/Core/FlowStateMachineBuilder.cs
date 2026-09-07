using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Core.Logging;
using AbilityKit.Game.View.Flow;
using AbilityKit.HFSM;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 从 <c>GameFlowDomain</c> 提取的状态机构建器（Step 4.7a）。
    /// 负责 Root / Battle 双层 HFSM 的状态注册、转移配置和条件求值。
    /// 纯逻辑，零 Unity 依赖，可独立测试。
    /// </summary>
    internal sealed class FlowStateMachineBuilder
    {
        private readonly MobaFlowConfiguration _flowConfig;
        private readonly MobaFlowConditionResolver _conditionResolver;
        private readonly ILogSink _log;
        private readonly IPresentationSink _presentationSink;
        private readonly FlowStateMachineCallbacks _callbacks;

        /// <summary>
        /// 状态机构建器需要的回调集合，由 Domain 提供。
        /// </summary>
        internal sealed class FlowStateMachineCallbacks
        {
            /// <summary>Root 状态进入时调用（更新 activeRoot + 触发 stateBindings + 推送事件）。</summary>
            public Action<MobaRootState> OnRootStateEntered { get; set; }

            /// <summary>Battle 子状态进入时调用（日志 + stateBindings）。</summary>
            public Action<MobaBattleState> OnBattleStateEntered { get; set; }

            /// <summary>Battle 子状态变化时调用（更新 activeBattle + 推送事件）。</summary>
            public Action<MobaBattleState> OnBattleStateChanged { get; set; }

            /// <summary>Root 状态进入后由 stateBindings.Enter 调用。</summary>
            public Action<MobaRootState> EnterRootBindings { get; set; }

            /// <summary>Root 状态退出后由 stateBindings.Exit 调用。</summary>
            public Action<MobaRootState> ExitRootBindings { get; set; }

            /// <summary>Battle 状态进入后由 stateBindings.Enter 调用。</summary>
            public Action<MobaBattleState> EnterBattleBindings { get; set; }

            /// <summary>Battle 状态退出后由 stateBindings.Exit 调用。</summary>
            public Action<MobaBattleState> ExitBattleBindings { get; set; }

            /// <summary>求值 Root 转移条件（从 Domain 的 scope + 字段合成 condition context）。</summary>
            public Func<string, bool> EvaluateRootCondition { get; set; }
        }

        internal FlowStateMachineBuilder(
            MobaFlowConfiguration flowConfig,
            MobaFlowConditionResolver conditionResolver,
            ILogSink log,
            IPresentationSink presentationSink,
            FlowStateMachineCallbacks callbacks)
        {
            _flowConfig = flowConfig ?? throw new ArgumentNullException(nameof(flowConfig));
            _conditionResolver = conditionResolver ?? throw new ArgumentNullException(nameof(conditionResolver));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _presentationSink = presentationSink ?? throw new ArgumentNullException(nameof(presentationSink));
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        }

        internal StateMachine<string, MobaRootState, MobaRootEvent> BuildRootStateMachine(
            StateMachine<MobaRootState, MobaBattleState, MobaBattleEvent> battleFsm)
        {
            var fsm = new StateMachine<string, MobaRootState, MobaRootEvent>();

            fsm.AddState(MobaRootState.Boot, new State<MobaRootState, MobaRootEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Boot);
                    _callbacks.EnterRootBindings(MobaRootState.Boot);
                    _presentationSink.OnPhaseChanged(MobaRootState.Boot, default);
                },
                onExit: _ => _callbacks.ExitRootBindings(MobaRootState.Boot)));

            fsm.AddState(MobaRootState.Lobby, new State<MobaRootState, MobaRootEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Lobby);
                    _callbacks.EnterRootBindings(MobaRootState.Lobby);
                    _presentationSink.OnPhaseChanged(MobaRootState.Lobby, default);
                },
                onExit: _ => _callbacks.ExitRootBindings(MobaRootState.Lobby)));

            fsm.AddState(MobaRootState.Battle, battleFsm);

            AddRootTransitions(fsm, _flowConfig.RootMachine);
            fsm.SetStartState(_flowConfig.RootMachine.StartState);
            return fsm;
        }

        internal StateMachine<MobaRootState, MobaBattleState, MobaBattleEvent> BuildBattleStateMachine()
        {
            var fsm = new StateMachine<MobaRootState, MobaBattleState, MobaBattleEvent>();

            fsm.StateChanged += s =>
            {
                _callbacks.OnBattleStateChanged(s.name);
                _presentationSink.OnPhaseChanged(MobaRootState.Battle, s.name);
            };

            fsm.AddState(MobaBattleState.Prepare, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.OnBattleStateEntered(MobaBattleState.Prepare);
                    _callbacks.EnterBattleBindings(MobaBattleState.Prepare);
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.Prepare)));

            fsm.AddState(MobaBattleState.Connect, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.OnBattleStateEntered(MobaBattleState.Connect);
                    _callbacks.EnterBattleBindings(MobaBattleState.Connect);
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.Connect)));

            fsm.AddState(MobaBattleState.CreateOrJoinWorld, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.OnBattleStateEntered(MobaBattleState.CreateOrJoinWorld);
                    _callbacks.EnterBattleBindings(MobaBattleState.CreateOrJoinWorld);
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.CreateOrJoinWorld)));

            fsm.AddState(MobaBattleState.LoadAssets, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.OnBattleStateEntered(MobaBattleState.LoadAssets);
                    _callbacks.EnterBattleBindings(MobaBattleState.LoadAssets);
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.LoadAssets)));

            fsm.AddState(MobaBattleState.InMatch, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.OnBattleStateEntered(MobaBattleState.InMatch);
                    _callbacks.EnterBattleBindings(MobaBattleState.InMatch);
                    _presentationSink.OnBattleStart();
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.InMatch)));

            fsm.AddState(MobaBattleState.End, new State<MobaBattleState, MobaBattleEvent>(
                onEnter: _ =>
                {
                    _callbacks.OnRootStateEntered(MobaRootState.Battle);
                    _callbacks.EnterBattleBindings(MobaBattleState.End);
                    _presentationSink.OnBattleEnd();
                },
                onExit: _ => _callbacks.ExitBattleBindings(MobaBattleState.End)));

            AddBattleTransitions(fsm, _flowConfig.BattleMachine);
            fsm.SetStartState(_flowConfig.BattleMachine.StartState);
            return fsm;
        }

        private void AddRootTransitions(
            StateMachine<string, MobaRootState, MobaRootEvent> fsm,
            PhaseStateMachineSpec<MobaRootState, MobaRootEvent> spec)
        {
            for (var i = 0; i < spec.Transitions.Count; i++)
            {
                var transition = spec.Transitions[i];
                if (string.IsNullOrEmpty(transition.ConditionId))
                {
                    fsm.AddTriggerTransition(transition.Trigger, new Transition<MobaRootState>(transition.From, transition.To));
                    continue;
                }

                fsm.AddTriggerTransition(
                    transition.Trigger,
                    new Transition<MobaRootState>(
                        transition.From,
                        transition.To,
                        condition: _ => _callbacks.EvaluateRootCondition(transition.ConditionId)));
            }
        }

        private static void AddBattleTransitions(
            StateMachine<MobaRootState, MobaBattleState, MobaBattleEvent> fsm,
            PhaseStateMachineSpec<MobaBattleState, MobaBattleEvent> spec)
        {
            for (var i = 0; i < spec.Transitions.Count; i++)
            {
                var transition = spec.Transitions[i];
                fsm.AddTriggerTransition(transition.Trigger, new Transition<MobaBattleState>(transition.From, transition.To));
            }
        }
    }
}

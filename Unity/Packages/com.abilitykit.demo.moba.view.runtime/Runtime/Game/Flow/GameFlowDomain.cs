using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.EC;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game;
using UnityEngine;
using UnityHFSM;
using static AbilityKit.Game.Flow.GameFlowDomain;

namespace AbilityKit.Game.Flow
{
    public sealed class GameFlowDomain
    {
        private readonly GameEntry _entry;
        private readonly GamePhaseContext _ctx;

        public enum RootState
        {
            Boot = 0,
            Lobby = 1,
            Battle = 2
        }

        private enum RootEvent
        {
            BootCompleted = 0,
            EnterBattle = 1,
            ReturnLobby = 2
        }

        private enum BattleState
        {
            Prepare = 0,
            Connect = 1,
            CreateOrJoinWorld = 2,
            LoadAssets = 3,
            InMatch = 4,
            End = 5
        }

        private enum BattleEvent
        {
            PrepareDone = 0,
            Connected = 1,
            JoinedWorld = 2,
            LoadingDone = 3,
            Ended = 4
        }

        private readonly FlowContext _flowContext;
        private readonly FlowEventQueue<RootEvent> _rootEvents;
        private readonly StateMachine<string, RootState, RootEvent> _root;
        private readonly HfsmFlowRunner<string, RootState, RootEvent> _runner;

        private readonly List<IGamePhaseFeature> _features = new List<IGamePhaseFeature>(16);
        private RootState _activeRoot;
        private BattleState _activeBattle;
        private bool _battleRequested;
        private IBattleBootstrapper _pendingBootstrapper;

        private BattleSessionFeature _battleSessionFeature;

        private StateMachine<RootState, BattleState, BattleEvent> _battleFsm;
        private bool _battleSessionStarted;
        private bool _battleFirstFrameReceived;

        public GameFlowDomain(GameEntry entry)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _ctx = new GamePhaseContext(_entry, _entry.Root);

            _flowContext = new FlowContext();
            _rootEvents = new FlowEventQueue<RootEvent>();
            _root = BuildRootStateMachine();
            _runner = new HfsmFlowRunner<string, RootState, RootEvent>(_flowContext, _root, _rootEvents);
        }

        public RootState CurrentPhase => _activeRoot;

        public void Start()
        {
            _runner.Start();
            _rootEvents.Enqueue(RootEvent.BootCompleted);
        }

        public void Tick(float deltaTime)
        {
            try
            {
                _runner.Step(deltaTime);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[GameFlowDomain] HFSM Step failed");
            }

            for (int i = 0; i < _features.Count; i++)
            {
                try
                {
                    _features[i].Tick(_ctx, deltaTime);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[GameFlowDomain] Feature.Tick failed: feature={_features[i]?.GetType().FullName}");
                }
            }
        }

        public void OnGUI()
        {
            for (int i = 0; i < _features.Count; i++)
            {
                if (_features[i] is IOnGUIFeature gui)
                {
                    gui.OnGUI(_ctx);
                }
            }

            if (!_entry.DebugEnabled) return;

            GUILayout.BeginArea(new Rect(350, 10, 420, 140), GUI.skin.window);
            GUILayout.Label($"HFSM Root={_activeRoot}, Battle={_activeBattle}");

            if (GUILayout.Button("Enter Battle", GUILayout.Height(28)))
            {
                EnterBattle((IBattleBootstrapper)null);
            }

            if (GUILayout.Button("Battle End", GUILayout.Height(28)))
            {
                var state = _root.GetState(RootState.Battle);
                if (state is StateMachine<RootState, BattleState, BattleEvent> battle)
                {
                    battle.Trigger(BattleEvent.Ended);
                }
            }

            if (GUILayout.Button("Return Lobby", GUILayout.Height(28)))
            {
                _rootEvents.Enqueue(RootEvent.ReturnLobby);
            }

            GUILayout.EndArea();
        }

        public void SwitchTo(IGamePhase next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            if (next is BattlePhase battle)
            {
                EnterBattle((IBattleBootstrapper)null);
                return;
            }

            ReturnToBoot();
        }

        public void Attach(IGamePhaseFeature feature)
        {
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            _features.Add(feature);

            if (_ctx.Root.IsValid)
            {
                _ctx.Root.AddComponent((object)feature);
            }

            feature.OnAttach(_ctx);
        }

        public void Detach(IGamePhaseFeature feature)
        {
            if (feature == null) return;

            feature.OnDetach(_ctx);

            if (_ctx.Root.IsValid)
            {
                _ctx.Root.RemoveComponent(feature.GetType());
            }
        }

        public void EnterBattle(IBattleBootstrapper bootstrapper)
        {
            _battleRequested = true;
            _pendingBootstrapper = bootstrapper;
            _rootEvents.Enqueue(RootEvent.EnterBattle);
        }

        public void ReturnToBoot()
        {
            _battleRequested = false;
            _pendingBootstrapper = null;
            _rootEvents.Enqueue(RootEvent.ReturnLobby);
        }

        private StateMachine<string, RootState, RootEvent> BuildRootStateMachine()
        {
            var fsm = new StateMachine<string, RootState, RootEvent>();

            fsm.AddState(RootState.Boot, onEnter: _ =>
            {
                _activeRoot = RootState.Boot;
                ClearFeatures();
                Attach(new BootMenuOnGUIFeature());
            });

            fsm.AddState(RootState.Lobby, onEnter: _ =>
            {
                _activeRoot = RootState.Lobby;
                ClearFeatures();
                Attach(new BootMenuOnGUIFeature());
            });

            var battle = BuildBattleStateMachine();
            fsm.AddState(RootState.Battle, battle);

            fsm.AddTriggerTransition(RootEvent.BootCompleted, RootState.Boot, RootState.Lobby);

            fsm.AddTriggerTransition(RootEvent.EnterBattle, RootState.Lobby, RootState.Battle, condition: _ => _battleRequested);
            fsm.AddTriggerTransition(RootEvent.EnterBattle, RootState.Boot, RootState.Battle, condition: _ => _battleRequested);

            fsm.AddTriggerTransition(RootEvent.ReturnLobby, RootState.Battle, RootState.Lobby);
            fsm.AddTriggerTransition(RootEvent.ReturnLobby, RootState.Boot, RootState.Lobby);

            fsm.SetStartState(RootState.Boot);
            return fsm;
        }

        private StateMachine<RootState, BattleState, BattleEvent> BuildBattleStateMachine()
        {
            var fsm = new StateMachine<RootState, BattleState, BattleEvent>();
            _battleFsm = fsm;

            fsm.StateChanged += s => { _activeBattle = s.name; };

            fsm.AddState(BattleState.Prepare, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;
                ClearFeatures();

                _battleSessionStarted = false;
                _battleFirstFrameReceived = false;

                Attach(new BattleContextFeature());
                Attach(new BattleEntityFeature());

                _battleSessionFeature = new BattleSessionFeature(_pendingBootstrapper);
                _battleSessionFeature.SessionStarted += OnBattleSessionStarted;
                _battleSessionFeature.FirstFrameReceived += OnBattleFirstFrameReceived;
                _battleSessionFeature.SessionFailed += OnBattleSessionFailed;
                Attach(_battleSessionFeature);

                Attach(new BattleDebugOnGUIFeature());
            });

            fsm.AddState(BattleState.Connect, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;

                Attach(new BattleDebugOnGUIFeature());

                if (_battleSessionStarted)
                {
                    fsm.Trigger(BattleEvent.Connected);
                }
            });

            fsm.AddState(BattleState.CreateOrJoinWorld, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;

                Attach(new BattleDebugOnGUIFeature());

                if (_battleFirstFrameReceived)
                {
                    fsm.Trigger(BattleEvent.JoinedWorld);
                }
            });

            fsm.AddState(BattleState.LoadAssets, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;

                // Waiting for BattleSessionFeature.FirstFrameReceived
                Attach(new BattleDebugOnGUIFeature());

                if (_battleFirstFrameReceived)
                {
                    fsm.Trigger(BattleEvent.LoadingDone);
                }
            });

            fsm.AddState(BattleState.InMatch, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;

                // Core battle features (context + session) already attached in Prepare.
                Attach(new BattleSyncFeature());
                Attach(new BattleInputFeature());
                Attach(new BattleViewFeature());
                Attach(new BattleHudFeature());
                Attach(new BattleDebugOnGUIFeature());
            });

            fsm.AddState(BattleState.End, onEnter: _ =>
            {
                _activeRoot = RootState.Battle;
                ClearFeatures();
                Attach(new BattleDebugOnGUIFeature());
                _rootEvents.Enqueue(RootEvent.ReturnLobby);

                _battleSessionFeature = null;
                _battleSessionStarted = false;
                _battleFirstFrameReceived = false;
            });

            fsm.AddTriggerTransition(BattleEvent.PrepareDone, BattleState.Prepare, BattleState.Connect);
            fsm.AddTriggerTransition(BattleEvent.Connected, BattleState.Connect, BattleState.CreateOrJoinWorld);
            fsm.AddTriggerTransition(BattleEvent.JoinedWorld, BattleState.CreateOrJoinWorld, BattleState.LoadAssets);
            fsm.AddTriggerTransition(BattleEvent.LoadingDone, BattleState.LoadAssets, BattleState.InMatch);
            fsm.AddTriggerTransition(BattleEvent.Ended, BattleState.InMatch, BattleState.End);

            fsm.SetStartState(BattleState.Prepare);
            return fsm;
        }

        private void OnBattleSessionStarted()
        {
            _battleSessionStarted = true;
            if (_battleFsm == null) return;

            if (_activeBattle == BattleState.Prepare)
            {
                _battleFsm.Trigger(BattleEvent.PrepareDone);
            }
            else if (_activeBattle == BattleState.Connect)
            {
                _battleFsm.Trigger(BattleEvent.Connected);
            }
        }

        private void OnBattleFirstFrameReceived()
        {
            _battleFirstFrameReceived = true;
            if (_battleFsm == null) return;

            if (_activeBattle == BattleState.CreateOrJoinWorld)
            {
                _battleFsm.Trigger(BattleEvent.JoinedWorld);
            }
            else if (_activeBattle == BattleState.LoadAssets)
            {
                _battleFsm.Trigger(BattleEvent.LoadingDone);
            }
        }

        private void OnBattleSessionFailed(Exception ex)
        {
            Log.Exception(ex, "[GameFlowDomain] Battle session failed");
            if (_battleFsm == null) return;

            if (_activeBattle != BattleState.End)
            {
                _battleFsm.Trigger(BattleEvent.Ended);
            }
        }

        private void ClearFeatures()
        {
            for (int i = _features.Count - 1; i >= 0; i--)
            {
                Detach(_features[i]);
            }
            _features.Clear();

            if (_battleSessionFeature != null)
            {
                _battleSessionFeature.SessionStarted -= OnBattleSessionStarted;
                _battleSessionFeature.FirstFrameReceived -= OnBattleFirstFrameReceived;
                _battleSessionFeature = null;
            }
        }
    }

    public readonly struct GamePhaseContext
    {
        public readonly GameEntry Entry;
        public readonly Entity Root;

        public GamePhaseContext(GameEntry entry, Entity root)
        {
            Entry = entry;
            Root = root;
        }
    }

    public interface IGamePhase
    {
        void Enter(in GamePhaseContext ctx);
        void Exit(in GamePhaseContext ctx);
        void Tick(in GamePhaseContext ctx, float deltaTime);
    }

    public interface IGamePhaseFeature
    {
        void OnAttach(in GamePhaseContext ctx);
        void OnDetach(in GamePhaseContext ctx);
        void Tick(in GamePhaseContext ctx, float deltaTime);
    }

    public interface IOnGUIFeature
    {
        void OnGUI(in GamePhaseContext ctx);
    }

    public sealed class BootPhase : IGamePhase
    {
        public void Enter(in GamePhaseContext ctx)
        {
            var flow = ctx.Entry.Get<GameFlowDomain>();
            flow.Attach(new BootMenuOnGUIFeature());
        }

        public void Exit(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }

    public sealed class BootMenuOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private bool _show = true;

        public void OnAttach(in GamePhaseContext ctx)
        {
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            if (!ctx.Entry.DebugEnabled) return;

            var flow = ctx.Entry.Get<GameFlowDomain>();
            if (flow != null && flow.CurrentPhase == RootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 120), GUI.skin.window);
            GUILayout.Label("Game Flow");

            if (GUILayout.Button("Enter Battle (Test)", GUILayout.Height(28)))
            {
                flow = ctx.Entry.Get<GameFlowDomain>();
                flow.EnterBattle(new TestBattleBootstrapper());
            }

            GUILayout.EndArea();
        }
    }
}

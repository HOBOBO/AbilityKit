using System;
using System.Collections.Generic;
using AbilityKit.Ability.EC;
using AbilityKit.Game;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class GameFlowDomain
    {
        private readonly GameEntry _entry;
        private readonly GamePhaseContext _ctx;

        private IGamePhase _phase;
        private readonly List<IGamePhaseFeature> _features = new List<IGamePhaseFeature>(16);

        public GameFlowDomain(GameEntry entry)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _ctx = new GamePhaseContext(_entry, _entry.Root);
        }

        public IGamePhase CurrentPhase => _phase;

        public void Start()
        {
            SwitchTo(new BootPhase());
        }

        public void Tick(float deltaTime)
        {
            _phase?.Tick(_ctx, deltaTime);

            for (int i = 0; i < _features.Count; i++)
            {
                _features[i].Tick(_ctx, deltaTime);
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
        }

        public void SwitchTo(IGamePhase next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            for (int i = _features.Count - 1; i >= 0; i--)
            {
                Detach(_features[i]);
            }
            _features.Clear();

            _phase?.Exit(_ctx);
            _phase = next;
            _phase.Enter(_ctx);
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
            SwitchTo(new BattlePhase(bootstrapper));
        }

        public void ReturnToBoot()
        {
            SwitchTo(new BootPhase());
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

            GUILayout.BeginArea(new Rect(10, 10, 320, 120), GUI.skin.window);
            GUILayout.Label("Game Flow");

            if (GUILayout.Button("Enter Battle (Test)", GUILayout.Height(28)))
            {
                var flow = ctx.Entry.Get<GameFlowDomain>();
                flow.EnterBattle(new TestBattleBootstrapper());
            }

            GUILayout.EndArea();
        }
    }
}

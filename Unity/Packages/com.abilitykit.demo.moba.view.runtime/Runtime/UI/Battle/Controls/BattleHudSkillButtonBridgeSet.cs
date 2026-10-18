using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    internal sealed class BattleHudSkillButtonBridgeSet
    {
        private readonly BattleHudSkillButtonBridgeSetFactory _factory;
        private readonly List<BattleHudSkillButtonEventBridge> _bridges;

        public BattleHudSkillButtonBridgeSet(BattleHudSkillButtonBridgeSetFactory factory = null)
        {
            _factory = factory ?? new BattleHudSkillButtonBridgeSetFactory();
            _bridges = new List<BattleHudSkillButtonEventBridge>(_factory.CreateBridges());

            for (int i = 0; i < _bridges.Count; i++)
            {
                HookBridge(_bridges[i]);
            }
        }

        public event Action<int> Click;
        public event Action<int> LongPress;
        public event Action<int, Vector2> AimStart;
        public event Action<int, Vector2> AimUpdate;
        public event Action<int, Vector2> AimEnd;
        public event Action AimCancel;

        public void Bind(IReadOnlyList<SkillButtonView> skills)
        {
            var count = skills != null ? skills.Count : 0;
            EnsureBridgeCount(count);

            for (int i = 0; i < _bridges.Count; i++)
            {
                var view = i < count ? skills[i] : null;
                _bridges[i].Bind(view, i + 1);
            }
        }

        public void Bind(SkillButtonView skill1, SkillButtonView skill2, SkillButtonView skill3)
        {
            Bind(new[] { skill1, skill2, skill3 });
        }

        public void Unbind()
        {
            for (int i = 0; i < _bridges.Count; i++)
            {
                _bridges[i].Unbind();
            }
        }

        private void EnsureBridgeCount(int count)
        {
            while (_bridges.Count < count)
            {
                var bridge = _factory.CreateBridge();
                HookBridge(bridge);
                _bridges.Add(bridge);
            }
        }

        private void HookBridge(BattleHudSkillButtonEventBridge bridge)
        {
            if (bridge == null) return;

            bridge.Click += OnClick;
            bridge.LongPress += OnLongPress;
            bridge.AimStart += OnAimStart;
            bridge.AimUpdate += OnAimUpdate;
            bridge.AimEnd += OnAimEnd;
            bridge.AimCancel += OnAimCancel;
        }

        private void OnClick(int slot)
        {
            Click?.Invoke(slot);
        }

        private void OnLongPress(int slot)
        {
            LongPress?.Invoke(slot);
        }

        private void OnAimStart(int slot, Vector2 aim)
        {
            AimStart?.Invoke(slot, aim);
        }

        private void OnAimUpdate(int slot, Vector2 aim)
        {
            AimUpdate?.Invoke(slot, aim);
        }

        private void OnAimEnd(int slot, Vector2 aim)
        {
            AimEnd?.Invoke(slot, aim);
        }

        private void OnAimCancel()
        {
            AimCancel?.Invoke();
        }
    }

    internal sealed class BattleHudSkillButtonBridgeSetFactory
    {
        public BattleHudSkillButtonEventBridge CreateBridge()
        {
            return new BattleHudSkillButtonEventBridge();
        }

        public BattleHudSkillButtonEventBridge[] CreateBridges()
        {
            return new[]
            {
                CreateBridge(),
                CreateBridge(),
                CreateBridge(),
            };
        }
    }
}

using System;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    internal sealed class BattleHudSkillButtonEventBridge
    {
        private SkillButtonView _view;
        private int _slot;

        public event Action<int> Click;
        public event Action<int> LongPress;
        public event Action<int, Vector2> AimStart;
        public event Action<int, Vector2> AimUpdate;
        public event Action<int, Vector2> AimEnd;
        public event Action AimCancel;

        public void Bind(SkillButtonView view, int slot)
        {
            Unbind();

            _view = view;
            _slot = slot;

            if (_view == null) return;

            _view.OnClick += OnClick;
            _view.OnLongPress += OnLongPress;
            _view.OnAimStart += OnAimStart;
            _view.OnAimUpdate += OnAimUpdate;
            _view.OnAimEnd += OnAimEnd;
            _view.OnAimCancel += OnAimCancel;
        }

        public void Unbind()
        {
            if (_view != null)
            {
                _view.OnClick -= OnClick;
                _view.OnLongPress -= OnLongPress;
                _view.OnAimStart -= OnAimStart;
                _view.OnAimUpdate -= OnAimUpdate;
                _view.OnAimEnd -= OnAimEnd;
                _view.OnAimCancel -= OnAimCancel;
            }

            _view = null;
            _slot = 0;
        }

        private void OnClick()
        {
            Click?.Invoke(_slot);
        }

        private void OnLongPress()
        {
            LongPress?.Invoke(_slot);
        }

        private void OnAimStart(Vector2 aim)
        {
            AimStart?.Invoke(_slot, aim);
        }

        private void OnAimUpdate(Vector2 aim)
        {
            AimUpdate?.Invoke(_slot, aim);
        }

        private void OnAimEnd(Vector2 aim)
        {
            AimEnd?.Invoke(_slot, aim);
        }

        private void OnAimCancel()
        {
            AimCancel?.Invoke();
        }
    }
}

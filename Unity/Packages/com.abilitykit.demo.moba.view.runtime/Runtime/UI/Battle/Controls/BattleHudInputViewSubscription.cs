using System;

namespace AbilityKit.Game.Battle.View
{
    internal sealed class BattleHudInputViewSubscription
    {
        private readonly Action<BattleHudInputView> _attach;
        private readonly Action<BattleHudInputView> _detach;

        private BattleHudInputView _hud;
        private bool _isHooked;

        public BattleHudInputViewSubscription(
            Action<BattleHudInputView> attach,
            Action<BattleHudInputView> detach)
        {
            _attach = attach;
            _detach = detach;
        }

        public void SetHud(BattleHudInputView hud)
        {
            if (_hud == hud) return;

            Unhook();
            _hud = hud;
        }

        public void Hook()
        {
            if (_isHooked) return;
            if (_hud == null) return;

            _attach?.Invoke(_hud);
            _isHooked = true;
        }

        public void Unhook()
        {
            if (!_isHooked) return;

            if (_hud != null)
            {
                _detach?.Invoke(_hud);
            }

            _isHooked = false;
        }

        public void Clear()
        {
            Unhook();
            _hud = null;
        }
    }
}

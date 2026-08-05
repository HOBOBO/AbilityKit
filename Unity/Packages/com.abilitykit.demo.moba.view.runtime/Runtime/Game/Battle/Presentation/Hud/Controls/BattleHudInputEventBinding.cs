namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputEventBinding
    {
        private readonly BattleHudInputEventSubscriptionList _subscriptions = new BattleHudInputEventSubscriptionList();
        private readonly BattleHudInputEventDispatcher _events;
        private BattleHudInputUi _ui;

        public BattleHudInputEventBinding(BattleHudInputEventDispatcher events)
        {
            _events = events;
        }

        public void Bind(BattleHudInputUi ui)
        {
            Unbind();
            _ui = ui;
            if (_ui == null || _events == null) return;

            if (_ui.MoveJoystick != null)
            {
                var joystick = _ui.MoveJoystick;
                _subscriptions.Add(
                    () => joystick.OnBegin += _events.OnMoveBegin,
                    () => joystick.OnBegin -= _events.OnMoveBegin);
                _subscriptions.Add(
                    () => joystick.OnEnd += _events.OnMoveEnd,
                    () => joystick.OnEnd -= _events.OnMoveEnd);
            }

            if (_ui.MoveMapper != null)
            {
                var moveMapper = _ui.MoveMapper;
                _subscriptions.Add(
                    () => moveMapper.MoveDxDzChanged += _events.OnMoveDxDzChanged,
                    () => moveMapper.MoveDxDzChanged -= _events.OnMoveDxDzChanged);
            }

            if (_ui.SkillAimMapper != null)
            {
                var skillAimMapper = _ui.SkillAimMapper;
                _subscriptions.Add(
                    () => skillAimMapper.SkillAimStart += _events.OnSkillAimStart,
                    () => skillAimMapper.SkillAimStart -= _events.OnSkillAimStart);
                _subscriptions.Add(
                    () => skillAimMapper.SkillAimUpdate += _events.OnSkillAimUpdate,
                    () => skillAimMapper.SkillAimUpdate -= _events.OnSkillAimUpdate);
                _subscriptions.Add(
                    () => skillAimMapper.SkillAimEnd += _events.OnSkillAimEnd,
                    () => skillAimMapper.SkillAimEnd -= _events.OnSkillAimEnd);
                _subscriptions.Add(
                    () => skillAimMapper.SkillAimCancel += _events.OnSkillAimCancel,
                    () => skillAimMapper.SkillAimCancel -= _events.OnSkillAimCancel);
            }

            if (_ui.InputView != null)
            {
                var inputView = _ui.InputView;
                _subscriptions.Add(
                    () => inputView.SkillClick += _events.OnSkillClick,
                    () => inputView.SkillClick -= _events.OnSkillClick);
            }
        }

        public void Unbind()
        {
            _subscriptions.Clear();
            _ui = null;
        }
    }
}

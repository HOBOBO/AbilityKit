using System;

namespace AbilityKit.Game.Battle
{
    public sealed class DefaultBattleLogicSessionRegistry : IBattleLogicSessionRegistry
    {
        private readonly bool _publishDebugFacade;
        private BattleLogicSession _current;
        private DefaultBattleDebugFacade _debugFacade;
        private string _debugScope = string.Empty;

        public DefaultBattleLogicSessionRegistry(bool publishDebugFacade = true)
        {
            _publishDebugFacade = publishDebugFacade;
        }

        public event Action<BattleLogicSession> SessionChanged;

        public BattleLogicSession Current => _current;
        public bool HasSession => _current != null;

        public BattleLogicSession Start(BattleLogicSessionOptions options, IBattleLogicTransport remoteTransport = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            Stop();
            if (_publishDebugFacade)
            {
                _debugFacade ??= new DefaultBattleDebugFacade(() => _current);
                _debugScope = options.WorldId.Value ?? string.Empty;
                BattleDebugFacadeProvider.Publish(_debugScope, _debugFacade);
                BattleDebugFacadeProvider.Current = _debugFacade;
            }

            _current = new BattleLogicSession(options, remoteTransport);
            SessionChanged?.Invoke(_current);
            return _current;
        }

        public void Stop()
        {
            if (_current == null)
            {
                ClearDebugFacade();
                return;
            }

            try
            {
                _current.Dispose();
            }
            finally
            {
                _current = null;
                ClearDebugFacade();
                SessionChanged?.Invoke(null);
            }
        }

        private void ClearDebugFacade()
        {
            if (!_publishDebugFacade) return;

            BattleDebugFacadeProvider.Withdraw(_debugScope, _debugFacade);
            _debugScope = string.Empty;
            if (ReferenceEquals(BattleDebugFacadeProvider.Current, _debugFacade))
            {
                BattleDebugFacadeProvider.Current = null;
            }
        }
    }
}

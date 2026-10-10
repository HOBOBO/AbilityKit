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

            try { Stop(); }
            catch (Exception stopFailure)
            {
                try { (remoteTransport as IDisposable)?.Dispose(); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Previous session cleanup and new transport disposal failed.",
                        stopFailure, cleanupFailure);
                }
                throw;
            }
            var session = new BattleLogicSession(options, remoteTransport);
            _current = session;
            try
            {
                if (_publishDebugFacade)
                {
                    _debugFacade ??= new DefaultBattleDebugFacade(() => _current);
                    _debugScope = options.WorldId.Value ?? string.Empty;
                    BattleDebugFacadeProvider.Publish(_debugScope, _debugFacade);
                    BattleDebugFacadeProvider.Current = _debugFacade;
                }

                SessionChanged?.Invoke(session);
                return session;
            }
            catch (Exception startupFailure)
            {
                try { Stop(); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Battle session publication and cleanup failed.",
                        startupFailure, cleanupFailure);
                }
                throw;
            }
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

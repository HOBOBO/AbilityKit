using System;

namespace AbilityKit.Game.Battle
{
    public static class BattleLogicSessionHost
    {
        private static BattleLogicSession _current;

        public static event Action<BattleLogicSession> SessionChanged;

        public static BattleLogicSession Current => _current;

        public static bool HasSession => _current != null;

        public static BattleLogicSession Start(BattleLogicSessionOptions options, IBattleLogicTransport remoteTransport = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (BattleDebugFacadeProvider.Current == null)
            {
                BattleDebugFacadeProvider.Current = new DefaultBattleDebugFacade();
            }

            Stop();

            _current = new BattleLogicSession(options, remoteTransport);
            SessionChanged?.Invoke(_current);
            return _current;
        }

        public static void Stop()
        {
            if (_current == null) return;

            try
            {
                _current.Dispose();
            }
            finally
            {
                _current = null;
                SessionChanged?.Invoke(null);
            }
        }
    }
}

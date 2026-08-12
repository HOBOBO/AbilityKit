namespace AbilityKit.Game.Flow
{
    internal sealed class TickLoopController
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly ITickLoopHost _host;

        public TickLoopController(BattleSessionState state, BattleSessionHandles handles, ITickLoopHost host)
        {
            _state = state;
            _handles = handles;
            _host = host;
        }

        public void MainTick(float deltaTime)
        {
            if (!HasSession()) return;

            var fixedDelta = _host.GetFixedDeltaSeconds();
            if (fixedDelta <= 0f) return;

            AccumulateDelta(deltaTime);
            TickMainSession(fixedDelta);
            TickAuxiliaryWorlds(deltaTime);
        }

        private bool HasSession()
        {
            return _handles.Session != null;
        }

        private void AccumulateDelta(float deltaTime)
        {
            _state.Tick.TickAcc += deltaTime;
        }

        private void TickMainSession(float fixedDelta)
        {
            while (_state.Tick.TickAcc >= fixedDelta)
            {
                TickNextFrame(fixedDelta);
            }
        }

        private void TickNextFrame(float fixedDelta)
        {
            var nextFrame = _state.Tick.LastFrame + 1;
            _handles.Session.Tick(fixedDelta);

            _state.Tick.LastFrame = nextFrame;
            _state.Tick.TickAcc -= fixedDelta;
        }

        private void TickAuxiliaryWorlds(float deltaTime)
        {
            _host.TickRemoteDrivenLocalSim(deltaTime);
            _host.TickConfirmedAuthorityWorldSim(deltaTime);
            _host.TickRemoteInterpolation(deltaTime);
        }
    }
}

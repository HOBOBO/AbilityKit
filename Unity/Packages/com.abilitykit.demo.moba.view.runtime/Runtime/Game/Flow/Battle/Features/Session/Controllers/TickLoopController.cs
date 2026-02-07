namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
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
                if (_handles.Session == null) return;

                if (!_state.Flags.TickEnteredLogged)
                {
                    _state.Flags.TickEnteredLogged = true;
                }

                _state.Tick.TickAcc += deltaTime;
                var fixedDelta = _host.GetFixedDeltaSeconds();
                while (_state.Tick.TickAcc >= fixedDelta)
                {
                    var nextFrame = _state.Tick.LastFrame + 1;
                    _handles.Replay.Driver?.Pump(_handles.Session, nextFrame);
                    _handles.Session.Tick(fixedDelta);
                    _state.Tick.LastFrame = nextFrame;
                    _state.Tick.TickAcc -= fixedDelta;
                }

                _host.TickRemoteDrivenLocalSim(deltaTime);
                _host.TickConfirmedAuthorityWorldSim(deltaTime);
            }
        }
    }
}

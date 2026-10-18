using System;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class TeamDeathmatchHudModule : IGameplayHudModule
    {
        private readonly BattleHudMatchClock _clock = new BattleHudMatchClock();
        private readonly BattleHudMatchState _match = new BattleHudMatchState();
        private readonly BattleHudRespawnClock _respawn = new BattleHudRespawnClock();
        private BattleGameplayPresentationState _state;

        public void Bind(RectTransform root, BattleGameplayPresentationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Dispose();
            _state = state;
            _clock.Bind(root);
            _match.Bind(root);
            _respawn.Bind(root);
            _state.ClockChanged += OnClock;
            _state.MatchChanged += OnMatch;
            _state.RespawnsChanged += OnRespawns;
            _state.Cleared += OnCleared;
            if (_state.HasClock) OnClock(_state.Clock);
            if (_state.Deathmatch.HasMatch) OnMatch(_state.Deathmatch.Match);
            OnRespawns(_state.Deathmatch.Respawns);
        }

        public void SetLocalActorId(int actorId) => _respawn.SetLocalActorId(actorId);

        private void OnClock(GameplayClockData value) => _clock.Apply(in value);
        private void OnMatch(GameplayMatchData value) => _match.Apply(in value);
        private void OnRespawns(GameplayRespawnData[] value) => _respawn.Apply(value);

        private void OnCleared()
        {
            _clock.Apply(default);
            _match.Apply(default);
            _respawn.Apply(Array.Empty<GameplayRespawnData>());
        }

        public void Dispose()
        {
            if (_state != null)
            {
                _state.ClockChanged -= OnClock;
                _state.MatchChanged -= OnMatch;
                _state.RespawnsChanged -= OnRespawns;
                _state.Cleared -= OnCleared;
            }
            _state = null;
            _clock.Dispose();
            _match.Dispose();
            _respawn.Dispose();
        }
    }
}

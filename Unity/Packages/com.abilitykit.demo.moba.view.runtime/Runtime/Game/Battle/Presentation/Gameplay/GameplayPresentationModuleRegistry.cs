using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Presentation.Features.Settlement;
using UnityEngine;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow
{
    internal interface IGameplayHudModule : IDisposable
    {
        void Bind(RectTransform root, BattleGameplayPresentationState state);
        void SetLocalActorId(int actorId);
    }

    internal interface IGameplayProjectionModule : IDisposable
    {
        void Bind(FrameSnapshotDispatcher snapshots, BattleGameplayPresentationState state);
    }

    internal sealed class GameplayPresentationModuleRegistry
    {
        public static readonly GameplayPresentationModuleRegistry Default = CreateStandard();

        public static GameplayPresentationModuleRegistry CreateStandard() =>
            new GameplayPresentationModuleRegistry()
                .RegisterProjection("team_deathmatch", () => new TeamDeathmatchProjectionModule())
                .RegisterHud("team_deathmatch", () => new TeamDeathmatchHudModule())
                .RegisterSettlement("team_deathmatch", () => new BattleEndSettlementFeature());

        private readonly Dictionary<string, Func<IGameplayHudModule>> _hud =
            new Dictionary<string, Func<IGameplayHudModule>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<IGameplayProjectionModule>> _projection =
            new Dictionary<string, Func<IGameplayProjectionModule>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<IGamePhaseFeature>> _settlement =
            new Dictionary<string, Func<IGamePhaseFeature>>(StringComparer.Ordinal);

        public GameplayPresentationModuleRegistry RegisterProjection(string id,
            Func<IGameplayProjectionModule> factory)
        {
            if (string.IsNullOrWhiteSpace(id) || factory == null || _projection.ContainsKey(id))
                throw new ArgumentException("Invalid or duplicate gameplay projection module: " + id);
            _projection.Add(id, factory);
            return this;
        }

        public IGameplayProjectionModule CreateProjection(string id) =>
            _projection.TryGetValue(id, out var factory)
                ? factory()
                : throw new InvalidOperationException("Gameplay projection module is missing: " + id);

        public GameplayPresentationModuleRegistry RegisterHud(string id, Func<IGameplayHudModule> factory)
        {
            if (string.IsNullOrWhiteSpace(id) || factory == null || _hud.ContainsKey(id))
                throw new ArgumentException("Invalid or duplicate gameplay HUD module: " + id);
            _hud.Add(id, factory);
            return this;
        }

        public GameplayPresentationModuleRegistry RegisterSettlement(string id, Func<IGamePhaseFeature> factory)
        {
            if (string.IsNullOrWhiteSpace(id) || factory == null || _settlement.ContainsKey(id))
                throw new ArgumentException("Invalid or duplicate gameplay settlement module: " + id);
            _settlement.Add(id, factory);
            return this;
        }

        public IGameplayHudModule CreateHud(string id) =>
            _hud.TryGetValue(id, out var factory)
                ? factory()
                : throw new InvalidOperationException("Gameplay HUD module is missing: " + id);

        public IGamePhaseFeature CreateSettlement(string id) =>
            _settlement.TryGetValue(id, out var factory)
                ? factory()
                : throw new InvalidOperationException("Gameplay settlement module is missing: " + id);

        public void Validate(GameplayPresentationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!_projection.ContainsKey(profile.ProjectionModuleId))
                throw new InvalidOperationException($"Gameplay {profile.GameplayId} projection module is missing: {profile.ProjectionModuleId}");
            if (!_hud.ContainsKey(profile.HudModuleId))
                throw new InvalidOperationException($"Gameplay {profile.GameplayId} HUD module is missing: {profile.HudModuleId}");
            if (!_settlement.ContainsKey(profile.SettlementModuleId))
                throw new InvalidOperationException($"Gameplay {profile.GameplayId} settlement module is missing: {profile.SettlementModuleId}");
        }
    }

    internal sealed class TeamDeathmatchProjectionModule : IGameplayProjectionModule
    {
        private IDisposable _match;
        private IDisposable _respawn;

        public void Bind(FrameSnapshotDispatcher snapshots, BattleGameplayPresentationState state)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (state == null) throw new ArgumentNullException(nameof(state));
            Dispose();
            _match = snapshots.Subscribe<GameplayMatchData>(GameplayMatchSnapshotRoute.OpCode,
                (_, value) => state.Apply(in value));
            _respawn = snapshots.Subscribe<GameplayRespawnData[]>(GameplayRespawnSnapshotRoute.OpCode,
                (_, value) => state.Apply(value));
        }

        public void Dispose()
        {
            _match?.Dispose();
            _respawn?.Dispose();
            _match = null;
            _respawn = null;
        }
    }

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

        private void OnClock(AbilityKit.Demo.Moba.Share.GameplayClockData value) => _clock.Apply(in value);
        private void OnMatch(AbilityKit.Demo.Moba.Share.GameplayMatchData value) => _match.Apply(in value);
        private void OnRespawns(AbilityKit.Demo.Moba.Share.GameplayRespawnData[] value) => _respawn.Apply(value);

        private void OnCleared()
        {
            _clock.Apply(default);
            _match.Apply(default);
            _respawn.Apply(Array.Empty<AbilityKit.Demo.Moba.Share.GameplayRespawnData>());
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
